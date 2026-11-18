package app.mealprep.companion

import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.json.*
import java.util.UUID
import java.time.LocalDate

class AssistantRepository(private val repository:CompanionRepository,private val graph:AppGraph){
 val workspace=MutableStateFlow(AWorkspace())
 val busy=MutableStateFlow(false);val message=MutableStateFlow<Int?>(null)
 val capabilities=MutableStateFlow<ACapabilities?>(null);val consent=MutableStateFlow(false)
 val jobs=MutableStateFlow<List<AJob>>(emptyList());val browser=MutableStateFlow<List<ABrowseRecipe>>(emptyList())
 val preview=MutableStateFlow<APreview?>(null)
 private val dao=repository.dao
 private fun active(){check(graph.session.value?.accountKey==repository.session.accountKey)}
 private fun api():ApiClient{active();val session=graph.vault.read()!!;return ApiClient(session.baseUrl,session.token)}
 suspend fun restore(){active();dao.assistantState()?.let{workspace.value=WireJson.decodeFromString(it.payload)}}
 suspend fun persist(){active();dao.putAssistant(AssistantStateEntity(payload=WireJson.encodeToString(workspace.value)))}
 fun edit(change:(AWorkspace)->AWorkspace){workspace.value=change(workspace.value)}
 suspend fun run(block:suspend()->Unit){if(busy.value)return;busy.value=true;message.value=null;try{block()}catch(e:Exception){if(e is CancellationException)throw e;message.value=when{e is ApiFailure&&e.status==429->R.string.ai_quota;e is ApiFailure&&e.status==409->R.string.ai_conflict;e is ApiFailure&&e.status==401->R.string.session_expired;e is PendingReviewRequired->R.string.ai_pending;else->R.string.ai_unavailable}}finally{busy.value=false;persist()}}
 suspend fun refresh(){
  capabilities.value=WireJson.decodeFromString(api().call("GET","/v1/ai/capabilities"))
  val preferences=WireJson.decodeFromString<APreferenceResponse>(api().call("GET","/v1/ai/preferences"));consent.value=preferences.noticeVersion=="gemini-free-1"&&preferences.acceptedAt!=null
  if(workspace.value.context==null)edit{it.copy(preferences=WireJson.decodeFromString(preferences.preferencesJson))}
  context();recent();browse("")
 }
 suspend fun context(){val w=workspace.value;val context=WireJson.decodeFromString<AContext>(api().call("GET","/v1/ai/context?date=${w.startDate}&servings=${w.preferences.servings}"));edit{it.copy(context=context)}}
 suspend fun recent(){jobs.value=WireJson.decodeFromString(api().call("GET","/v1/ai/jobs"))}
 suspend fun browse(search:String){browser.value=WireJson.decodeFromString(api().call("GET","/v1/recipes?search="+java.net.URLEncoder.encode(search,"UTF-8")))}
 suspend fun openRecipe(id:Int){
  val draft=try{WireJson.decodeFromString<AFullRecipe>(api().call("GET","/v1/recipes/$id")).draft()}
   catch(e:java.io.IOException){workspace.value.context?.candidates?.find{it.recipeId==id}?.draft()?:throw e}
  edit{it.copy(recipes=it.recipes+draft)}
 }
 suspend fun importPreview(){val body=buildJsonObject{put("source",workspace.value.source)};val recipe=WireJson.decodeFromString<ARecipe>(api().call("POST","/v1/recipes/import-preview",body.toString()));edit{it.copy(recipes=it.recipes+ARecipeDraft(recipe))};message.value=R.string.ai_review}
 suspend fun setConsent(accepted:Boolean){api().call("POST","/v1/ai/consent",WireJson.encodeToString(AConsent(accepted=accepted)));consent.value=accepted}
 suspend fun savePreferences(){api().call("PUT","/v1/ai/preferences",WireJson.encodeToString(workspace.value.preferences));message.value=R.string.ai_saved}
 suspend fun upload(bytes:ByteArray){check(consent.value);val id=api().uploadImage(bytes);edit{it.copy(uploadId=id)};message.value=R.string.ai_image_ready}
 suspend fun generate(language:String){
  check(consent.value);val w=workspace.value;val request=ARequest(kind=w.kind,language=language,prompt=w.prompt,startDate=w.startDate,preferences=w.preferences,source=w.source,uploadId=w.uploadId,allowNewRecipes=w.allowNewRecipes,replaceEntryIds=w.replaceIds,pinnedMeals=w.pinned.mapNotNull{w.meals.getOrNull(it)})
  val job=WireJson.decodeFromString<AJob>(mutation("/v1/ai/jobs",WireJson.encodeToString(request),"job"));poll(job.jobId)
 }
 suspend fun poll(id:String){
  var job=WireJson.decodeFromString<AJob>(api().call("GET","/v1/ai/jobs/$id"));edit{it.copy(job=job)};persist()
  while(job.state in listOf("Queued","Running")){delay(2000);active();job=WireJson.decodeFromString(api().call("GET","/v1/ai/jobs/$id"));edit{it.copy(job=job)};persist()}
  if(job.state=="Completed"&&job.resultJson!=null){val p=WireJson.decodeFromString<AProposal>(job.resultJson!!);val context=job.workJson?.let{WireJson.parseToJsonElement(it).jsonObject["context"]?.let{v->WireJson.decodeFromJsonElement<AContext>(v)}}?:workspace.value.context
   edit{it.copy(context=context,proposalContext=context,suggestions=p.suggestions,recipes=p.recipes.map{r->ARecipeDraft(r)},meals=p.meals,purchases=p.purchases,selectedPurchases=p.purchases.indices.toList(),acceptedPurchases=emptyList(),warnings=p.warnings,pinned=emptyList(),planOperation=UUID.randomUUID().toString(),receiptOperation=UUID.randomUUID().toString())};message.value=R.string.ai_review
  }else message.value=if(job.errorCode=="QuotaExceeded")R.string.ai_quota else R.string.ai_unavailable
  if(job.kind=="receipt"){val accepted=WireJson.decodeFromString<List<Int>>(api().call("GET","/v1/ai/jobs/${job.jobId}/purchases"));edit{it.copy(acceptedPurchases=accepted,selectedPurchases=it.selectedPurchases-accepted.toSet())}}
  recent()
 }
 suspend fun cancel(){val id=workspace.value.job?.jobId?:return;api().call("POST","/v1/ai/jobs/$id/cancel");message.value=R.string.ai_cancelled}
 private suspend fun mutation(path:String,payload:String,action:String,index:Int?=null):String{
  val pending=APending(path,payload,action,index);workspace.value.pending?.let{if(it!=pending)throw PendingReviewRequired()}
  edit{it.copy(pending=pending)};persist()
  val result=try{api().call("POST",path,payload)}catch(e:ApiFailure){if(e.status in 400..499&&e.status!=401&&e.status!=429){edit{it.copy(pending=null)};persist()};throw e}
  complete(pending,result);persist();return result
 }
 private fun complete(pending:APending,result:String){
  when(pending.action){
   "job"->edit{it.copy(job=WireJson.decodeFromString<AJob>(result))}
   "recipe"->{val saved=WireJson.decodeFromString<ARecipeSaved>(result);edit{w->w.copy(recipes=w.recipes.mapIndexed{i,r->if(i==pending.recipeIndex)r.copy(recipeId=saved.id,publishedId=saved.id,version=saved.version,operationId=UUID.randomUUID().toString())else r})}}
   "plan"->edit{it.copy(meals=emptyList(),replaceIds=emptyList(),pinned=emptyList(),planOperation=UUID.randomUUID().toString())}
   "receipt"->edit{it.copy(acceptedPurchases=(it.acceptedPurchases+it.selectedPurchases).distinct(),selectedPurchases=emptyList(),receiptOperation=UUID.randomUUID().toString())}
  };edit{it.copy(pending=null)};message.value=R.string.ai_saved
 }
 suspend fun recover(){val pending=workspace.value.pending?:return;mutation(pending.path,pending.payload,pending.action,pending.recipeIndex);if(pending.action=="job")workspace.value.job?.let{poll(it.jobId)}else context()}
 suspend fun saveRecipe(index:Int){
  val draft=workspace.value.recipes[index];val r=draft.recipe
  require(r.title.isNotBlank()&&r.instructions.isNotBlank()&&r.ingredients.isNotEmpty());val lines=r.ingredients.map{ARecipeLineInput(requireNotNull(it.ingredientId),requireNotNull(it.unitId),Quantities.validate(requireNotNull(it.quantity)),it.notes)}
  val body=ARecipeSave(draft.operationId,ARecipeInput(draft.recipeId,r.categoryId,r.title,r.description,r.instructions,r.prepMinutes,r.cookMinutes,r.servings,lines,draft.version),r.source?:workspace.value.source.takeIf{draft.recipeId==null},workspace.value.kind)
  mutation("/v1/recipes/save",WireJson.encodeToString(body),"recipe",index)
 }
 private fun planInput():APlanApply{
  val w=workspace.value;val context=requireNotNull(w.proposalContext?:w.context);val meals=w.meals.map{m->if(m.draftIndex!=null)m.copy(recipeId=requireNotNull(w.recipes.getOrNull(m.draftIndex)?.publishedId),draftIndex=null)else m}
  val versions=meals.map{requireNotNull(it.recipeId)}.distinct().associateWith{id->val version=context.candidates.find{it.recipeId==id}?.version?:w.recipes.find{it.publishedId==id}?.version;val bytes=android.util.Base64.decode(requireNotNull(version),android.util.Base64.DEFAULT);"0x"+bytes.joinToString(""){"%02X".format(it)}}
  return APlanApply(w.planOperation,w.job?.takeIf{it.kind=="plan"}?.jobId,meals,w.replaceIds,versions,context.plan.associate{it.id to it.version},context.batches.associate{it.id to it.version})
 }
 suspend fun applyPlan(){mutation("/v1/plans/apply",WireJson.encodeToString(planInput()),"plan");context()}
 suspend fun previewPlan(){preview.value=WireJson.decodeFromString(api().call("POST","/v1/plans/preview",WireJson.encodeToString(planInput())))}
 suspend fun acceptPurchases(){val w=workspace.value;val job=requireNotNull(w.job);require(job.kind=="receipt");w.selectedPurchases.forEach{i->val p=w.purchases[i];require(p.ingredientId!=null&&p.unitId!=null&&p.purchasedOn!=null);Quantities.validate(requireNotNull(p.quantity));LocalDate.parse(p.purchasedOn)}
  mutation("/v1/ai/jobs/${job.jobId}/purchases",WireJson.encodeToString(AReceiptApply(w.receiptOperation,job.version,w.selectedPurchases,w.purchases,confirmDuplicate=w.confirmedDuplicate)),"receipt")
 }
 suspend fun createIngredient(recipeIndex:Int,lineIndex:Int){val line=workspace.value.recipes[recipeIndex].recipe.ingredients[lineIndex];require(line.name.isNotBlank()&&line.unitId!=null)
  val body=buildJsonObject{put("operationId",UUID.randomUUID().toString());put("name",line.name);put("unitId",line.unitId!!)}.toString()
  val result=WireJson.decodeFromString<MutationAck>(mutation("/v1/ingredients",body,"ingredient"));context();editRecipe(recipeIndex){r->r.copy(ingredients=r.ingredients.mapIndexed{i,l->if(i==lineIndex)l.copy(ingredientId=result.id)else l})}
 }
 fun editRecipe(index:Int,change:(ARecipe)->ARecipe){edit{w->w.copy(recipes=w.recipes.mapIndexed{i,r->if(i==index)r.copy(recipe=change(r.recipe))else r})}}
}
