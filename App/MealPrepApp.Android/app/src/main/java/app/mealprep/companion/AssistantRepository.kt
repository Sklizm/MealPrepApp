package app.mealprep.companion

import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.json.*
import java.util.UUID
import java.time.LocalDate

private class RecipeReviewFailure(val messageResource: Int) : IllegalArgumentException()

internal fun ALine.reviewQuantity() = quantityText?.let { Quantities.input(it) }
 ?: Quantities.validate(requireNotNull(quantity))

internal fun ARecipe.rowProblems(): Map<String, Int> {
 val duplicates = ingredients.mapNotNull { it.ingredientId }.groupingBy { it }.eachCount()
 return ingredients.mapNotNull { row ->
  val problem = when {
   row.ingredientId == null || row.unitId == null -> R.string.ai_mapping_required
   duplicates.getOrDefault(row.ingredientId, 0) > 1 -> R.string.ai_recipe_duplicate_ingredient
   runCatching { row.reviewQuantity() }.isFailure -> R.string.invalid_quantity
   else -> null
  }
  problem?.let { row.localId to it }
 }.toMap()
}

internal fun ARecipe.reviewProblem(): Int? = when {
 servings == null || servings !in 1..1000 -> R.string.ai_recipe_servings_required
 R.string.ai_recipe_duplicate_ingredient in rowProblems().values -> R.string.ai_recipe_duplicate_ingredient
 title.isBlank() || instructions.isBlank() || ingredients.isEmpty() || rowProblems().isNotEmpty() ->
  R.string.ai_recipe_review_required
 else -> null
}

internal fun assistantError(code: String?, status: Int? = null): Int = when (code) {
 "InvalidCredentials" -> R.string.invalid_credentials
 "QuotaExceeded" -> R.string.ai_quota
 "VersionConflict" -> R.string.ai_conflict
 "ConsentRequired" -> R.string.ai_accept_notice
 "RecipeServingsRequired" -> R.string.ai_recipe_servings_required
 "DuplicateRecipeIngredient" -> R.string.ai_recipe_duplicate_ingredient
 "ReviewRequired", "RecipeReviewRequired", "RequestRejected" -> R.string.ai_mapping_required
 "InvalidRecipePhoto", "UploadFailed" -> R.string.ai_invalid_photo
 "InvalidInput", "InvalidRequest" -> R.string.ai_invalid_input
 "InvalidModelOutput" -> R.string.ai_invalid_result
 "InvalidPlan", "StalePreview" -> R.string.ai_stale_preview
 "Cancelled" -> R.string.ai_cancelled
 "AccountLocked" -> R.string.account_locked
 "StorageUnavailable", "ProviderUnavailable", "ProviderAccessDenied" -> R.string.ai_unavailable
 "Disabled" -> R.string.ai_disabled
 "Refused", "IncompleteResponse", "TimedOut", "Interrupted" -> R.string.ai_invalid_result
 "ImageRequired", "InvalidUpload", "ImageExpired", "ImageUnavailable" -> R.string.ai_image_required
 "SessionExpired" -> R.string.session_expired
 else -> when (status) {
  400 -> R.string.ai_invalid_input
  401 -> R.string.session_expired
  403, 404 -> R.string.ai_access_unavailable
  409 -> R.string.ai_conflict
  423 -> R.string.account_locked
  429 -> R.string.ai_quota
  else -> R.string.ai_unavailable
 }
}

enum class RecipeSearchStatus { Loading, Current, Cached, Failed }
data class RecipeSearch(val query: String = "", val rows: List<ABrowseRecipe> = emptyList(),
 val status: RecipeSearchStatus = RecipeSearchStatus.Loading)


class AssistantRepository(private val repository:CompanionRepository,private val graph:AppGraph){
 val workspace=MutableStateFlow(AWorkspace())
 val busy=MutableStateFlow(false);val message=MutableStateFlow<Int?>(null)
 val capabilities=MutableStateFlow<ACapabilities?>(null);val consent=MutableStateFlow(false)
 val jobs=MutableStateFlow<List<AJob>>(emptyList());val browser=MutableStateFlow(RecipeSearch()); private var searchGeneration = 0
 val preview=MutableStateFlow<APreview?>(null)
 private val dao=repository.dao
 private var previewPayload: APlanApply? = null
 private var initialized = false
 private val persistGate = Mutex()
 val openedDraft = MutableStateFlow<String?>(null)
 val resultTab = MutableStateFlow<Int?>(null)
 private fun active(){check(graph.session.value?.accountKey==repository.session.accountKey)}
 private fun api():ApiClient{active();val session=graph.vault.read()!!;return ApiClient(session.baseUrl,session.token)}
 suspend fun restore(){
  if (initialized) return
  active()
  dao.assistantState()?.let { workspace.value=WireJson.decodeFromString(it.payload) }
  persist() // Persist generated identities for drafts created by earlier versions.
  initialized = true
 }
 suspend fun persist() = persistGate.withLock { active(); dao.putAssistant(AssistantStateEntity(payload=WireJson.encodeToString(workspace.value))) }
 fun edit(change:(AWorkspace)->AWorkspace){
  val old = workspace.value
  val updated = change(old)
  if (old.meals != updated.meals || old.replaceIds != updated.replaceIds ||
      old.startDate != updated.startDate || old.preferences != updated.preferences || old.context != updated.context ||
      old.proposalContext != updated.proposalContext || old.planJobId != updated.planJobId ||
      old.recipes.map { it.publishedId to it.version } != updated.recipes.map { it.publishedId to it.version }) {
   invalidatePreview()
  }
  workspace.value = updated
 }
 private fun invalidatePreview() {
  preview.value = null
  previewPayload = null
 }
 suspend fun run(block:suspend()->Unit){
  if(busy.value)return
  busy.value=true
  message.value=null
  try { block() }
  catch (e: Exception) {
   if (e is CancellationException) throw e
   if (e is ApiFailure && e.status == 409) invalidatePreview()
   message.value = when {
    e is RecipeReviewFailure -> e.messageResource
    e is ApiFailure && e.status == 401 -> R.string.session_expired
    e is PendingReviewRequired || workspace.value.pending != null -> R.string.ai_pending
    e is ApiFailure -> assistantError(e.code, e.status)
    e is java.io.IOException -> R.string.connection_required
    e is IllegalArgumentException -> R.string.ai_invalid_input
    else -> R.string.ai_unavailable
   }
  } finally {
   busy.value=false
   if (graph.session.value?.accountKey == repository.session.accountKey) persist()
  }
 }
 suspend fun refresh(){
  capabilities.value=WireJson.decodeFromString(api().call("GET","/v1/ai/capabilities"))
  val preferences=WireJson.decodeFromString<APreferenceResponse>(api().call("GET","/v1/ai/preferences"));consent.value=preferences.noticeVersion=="gemini-free-1"&&preferences.acceptedAt!=null
  if(workspace.value.context==null)edit{it.copy(preferences=WireJson.decodeFromString(preferences.preferencesJson))}
  context();recent()
 }
 suspend fun context(refreshPlan: Boolean = false){
  val w=workspace.value
  val servings=Quantities.validate(w.preferences.servings)
  val context=WireJson.decodeFromString<AContext>(api().call("GET","/v1/ai/context?date=${w.startDate}&servings=$servings"))
  invalidatePreview()
  edit{it.copy(context=context, proposalContext=if (refreshPlan) context else it.proposalContext)}
 }
 suspend fun recent(){jobs.value=WireJson.decodeFromString(api().call("GET","/v1/ai/jobs"))}
 suspend fun browse(search:String) {
  val query = search.trim()
  val generation = ++searchGeneration
  val previous = browser.value
  browser.value = RecipeSearch(query, previous.rows.takeIf { previous.query == query }.orEmpty())
  val slot = if (query.isEmpty()) "all" else "last"
  val stored = dao.cache("recipe-search").firstOrNull { it.id == slot }
  val cached = stored?.let { WireJson.decodeFromString<CachedRecipeSearch>(it.payload) }?.takeIf { it.query == query }?.rows
  if (generation != searchGeneration) return
  if (previous.query != query || previous.status != RecipeSearchStatus.Current)
   cached?.let { browser.value = RecipeSearch(query, it) }
  try {
   val rows = WireJson.decodeFromString<List<ABrowseRecipe>>(api().call("GET","/v1/recipes?search="+java.net.URLEncoder.encode(query,"UTF-8")))
   if (generation != searchGeneration) return
   dao.putCache(listOf(CacheEntity("recipe-search", slot, WireJson.encodeToString(CachedRecipeSearch(query, rows)))))
   if (generation == searchGeneration) browser.value = RecipeSearch(query, rows, RecipeSearchStatus.Current)
  } catch (e: Exception) {
   if (e is CancellationException) throw e
   if (generation != searchGeneration) return
   val rows = cached ?: previous.rows.takeIf { previous.query == query && previous.status != RecipeSearchStatus.Loading && previous.status != RecipeSearchStatus.Failed }
   browser.value = RecipeSearch(query, rows.orEmpty(), if (rows == null) RecipeSearchStatus.Failed else RecipeSearchStatus.Cached)
   if (e is ApiFailure && e.status == 401) message.value = R.string.session_expired
  }
 }
 fun selectDraft(id: String) { openedDraft.value = id }
 suspend fun openRecipe(id:Int){
  workspace.value.recipes.lastOrNull { it.recipeId == id }?.let {
   selectDraft(it.localId)
   return
  }
  val draft=try {
   val full = WireJson.decodeFromString<AFullRecipe>(api().call("GET","/v1/recipes/$id"))
   full.draft().copy(canEdit = full.userId == repository.session.userId)
  } catch(e:java.io.IOException) {
   if (e is ApiFailure && e.status !in 500..599) throw e
   workspace.value.context?.candidates?.find{it.recipeId==id}?.draft()?.copy(canEdit=false) ?: throw e
  }
  edit{it.copy(recipes=it.recipes+draft)}
  selectDraft(draft.localId)
 }
 private suspend fun photoDraft(recipe: ARecipe, kind: String): ARecipeDraft {
  val draft = ARecipeDraft(recipe.copy(photo = null), sourceKind = kind)
  val photo = recipe.photo ?: return draft
  dao.putCache(listOf(CacheEntity("recipe-photo", draft.operationId, WireJson.encodeToString(photo))))
  return draft.copy(photoKey = draft.operationId)
 }
 suspend fun recipePhoto(key: String?): ARecipePhoto? {
  if (key == null || graph.session.value?.accountKey != repository.session.accountKey) return null
  return dao.recipePhoto(key)?.let { WireJson.decodeFromString<ARecipePhoto>(it) }
 }
 suspend fun skipRecipePhoto(index: Int) {
  active()
  val photoKey = workspace.value.recipes[index].photoKey ?: return
  edit { state -> state.copy(recipes = state.recipes.mapIndexed { i, row ->
   if (i == index) row.copy(photoKey = null) else row
  }) }
  persist()
  dao.removeRecipePhotos(listOf(photoKey))
 }
 suspend fun importPreview() {
  val body = buildJsonObject { put("source", workspace.value.source) }
  val recipe = WireJson.decodeFromString<ARecipe>(api().call("POST", "/v1/recipes/import-preview", body.toString()))
  val draft = photoDraft(recipe, "import")
  edit { it.copy(recipes = it.recipes + draft) }
  selectDraft(draft.localId)
  resultTab.value = 1
  message.value = R.string.ai_review
 }
 suspend fun setConsent(accepted:Boolean){api().call("POST","/v1/ai/consent",WireJson.encodeToString(AConsent(accepted=accepted)));consent.value=accepted}
 suspend fun savePreferences(){api().call("PUT","/v1/ai/preferences",WireJson.encodeToString(workspace.value.preferences));message.value=R.string.ai_saved}
 suspend fun upload(bytes:ByteArray){check(consent.value);val id=api().uploadImage(bytes);edit{it.copy(uploadId=id,uploadKind=it.kind)};message.value=R.string.ai_image_ready}
 suspend fun generate(language:String){
  check(consent.value)
  val w=workspace.value
  val imageRequest = w.kind in setOf("receipt", "recipe-photo")
  if (imageRequest && w.uploadKind != w.kind) throw RecipeReviewFailure(R.string.ai_image_required)
  val planning = w.kind == "plan"
  val request=ARequest(kind=w.kind,language=language,prompt=w.prompt,startDate=w.startDate,
   preferences=w.preferences,source=w.source.takeIf { w.kind == "import" },
   uploadId=w.uploadId.takeIf { imageRequest },allowNewRecipes=planning && w.allowNewRecipes,
   replaceEntryIds=if (planning) w.replaceIds else emptyList(),
   pinnedMeals=if (planning) w.pinned.mapNotNull{w.meals.getOrNull(it)?.withWirePortions()} else emptyList())
  val job=WireJson.decodeFromString<AJob>(mutation("/v1/ai/jobs",WireJson.encodeToString(request),"job"))
  poll(job.jobId)
 }
 suspend fun poll(id:String){
  suspend fun get(): AJob {
   val job = WireJson.decodeFromString<AJob>(api().call("GET","/v1/ai/jobs/$id"))
   edit { it.copy(job=job.copy(workJson=null,resultJson=null)) }
   persist()
   return job
  }
  var job = get()
  while (job.state in listOf("Queued", "Running")) { delay(2000); job = get() }
  if (job.state != "Completed" || job.resultJson == null) {
   message.value = if (job.state == "Cancelled") R.string.ai_cancelled else assistantError(job.errorCode)
   return
  }
  if (workspace.value.pending != null) throw PendingReviewRequired()
  val proposal = WireJson.decodeFromString<AProposal>(job.resultJson!!)
  val sourceContext = job.workJson?.let {
   WireJson.parseToJsonElement(it).jsonObject["context"]?.let { value -> WireJson.decodeFromJsonElement<AContext>(value) }
  } ?: workspace.value.context
  val kind = job.kind
  val stateBefore = workspace.value
  val drafts = proposal.recipes.mapIndexed { index, recipe ->
   stateBefore.recipes.firstOrNull { it.originJobId == job.jobId && it.resultIndex == index }
    ?: photoDraft(recipe, kind).copy(originJobId=job.jobId,resultIndex=index)
  }
  val accepted = if (kind == "receipt") WireJson.decodeFromString<List<Int>>(
   api().call("GET","/v1/ai/jobs/${job.jobId}/purchases")
  ) else emptyList()
  edit { state ->
   val existingIds = state.recipes.map { it.localId }.toSet()
   val allDrafts = state.recipes + drafts.filterNot { it.localId in existingIds }
   val updated = state.copy(context=sourceContext,recipes=allDrafts,warnings=proposal.warnings)
   when (kind) {
    "suggest" -> updated.copy(suggestions=proposal.suggestions)
    "plan" -> if (state.planJobId == job.jobId) updated else updated.copy(proposalContext=sourceContext,planJobId=job.jobId,
     meals=proposal.meals.map { meal ->
      val draft = meal.draftIndex?.let(drafts::getOrNull)
      meal.copy(draftIndex=draft?.let { referenced -> allDrafts.indexOfFirst { it.localId == referenced.localId } })
     },
     pinned=emptyList(),planOperation=UUID.randomUUID().toString())
    "receipt" -> if (state.receiptJob?.jobId == job.jobId) updated.copy(
     acceptedPurchases=accepted,selectedPurchases=state.selectedPurchases-accepted.toSet()
    ) else updated.copy(receiptJob=job.copy(workJson=null,resultJson=null),purchases=proposal.purchases,
     selectedPurchases=proposal.purchases.indices.toList()-accepted.toSet(),acceptedPurchases=accepted,
     confirmedDuplicate=false,receiptOperation=UUID.randomUUID().toString())
    else -> updated
   }
  }
  persist()
  resultTab.value = when(kind) { "plan" -> 2; "receipt" -> 3; "suggest" -> 4; else -> 1 }
  if (resultTab.value == 1) drafts.firstOrNull()?.let { selectDraft(it.localId) }
  message.value = R.string.ai_review
  recent()
 }
 suspend fun cancel(){
  val id=workspace.value.job?.jobId?:return
  try { api().call("POST","/v1/ai/jobs/$id/cancel"); message.value=R.string.ai_cancelled }
  catch(e: ApiFailure) { message.value=assistantError(e.code,e.status) }
  catch(e: java.io.IOException) { message.value=R.string.connection_required }
 }
 private suspend fun mutation(path:String,payload:String,action:String,index:Int?=null,draftId:String?=null,ingredientRowId:String?=null):String{
  val pending=APending(path,payload,action,index,draftId,ingredientRowId);workspace.value.pending?.let{if(it!=pending)throw PendingReviewRequired()}
  edit{it.copy(pending=pending)};persist()
  val result=try{api().call("POST",path,payload)}catch(e:ApiFailure){if(e.status in 400..499&&e.status !in setOf(401,408,425,429)){edit{it.copy(pending=null)};persist()};throw e}
  complete(pending,result);persist();return result
 }
 private fun complete(pending:APending,result:String){
  when(pending.action){
   "job"->edit{it.copy(job=WireJson.decodeFromString<AJob>(result).copy(workJson=null,resultJson=null))}
   "recipe"->{val saved=WireJson.decodeFromString<ARecipeSaved>(result);edit{w->w.copy(recipes=w.recipes.mapIndexed{i,r->if(pending.draftId?.let { it == r.localId } ?: (i == pending.recipeIndex))r.copy(recipeId=saved.id,publishedId=saved.id,version=saved.version,operationId=UUID.randomUUID().toString())else r})}}
   "ingredient" -> {
    val id = WireJson.decodeFromString<MutationAck>(result).id
    edit { state -> state.copy(recipes=state.recipes.map { draft ->
     if (draft.localId != pending.draftId) draft else draft.copy(recipe=draft.recipe.copy(
      ingredients=draft.recipe.ingredients.map { line ->
       if (line.localId == pending.ingredientRowId && line.ingredientId == null) line.copy(ingredientId=id) else line
      }
     ))
    }) }
   }
   "plan"->edit{it.copy(meals=emptyList(),replaceIds=emptyList(),pinned=emptyList(),planOperation=UUID.randomUUID().toString())}
   "receipt"->edit{it.copy(acceptedPurchases=(it.acceptedPurchases+WireJson.decodeFromString<AReceiptApply>(pending.payload).rowIndexes).distinct(),selectedPurchases=emptyList(),receiptOperation=UUID.randomUUID().toString())}
  };edit{it.copy(pending=null)};message.value=R.string.ai_saved
 }
 suspend fun recover(){val pending=workspace.value.pending?:return;mutation(pending.path,pending.payload,pending.action,pending.recipeIndex,pending.draftId,pending.ingredientRowId);if(pending.action=="job")workspace.value.job?.let{poll(it.jobId)}else if (pending.action in setOf("plan","ingredient")) context()}
 suspend fun saveRecipe(index:Int){
  val draft=workspace.value.recipes[index];val r=draft.recipe
  if (!draft.canEdit) throw RecipeReviewFailure(R.string.ai_access_unavailable)
  r.reviewProblem()?.let { throw RecipeReviewFailure(it) }
  val lines=r.ingredients.map{ARecipeLineInput(requireNotNull(it.ingredientId),requireNotNull(it.unitId),it.reviewQuantity(),it.notes)}
  val photo = recipePhoto(draft.photoKey)
  if (draft.photoKey != null && photo == null) throw RecipeReviewFailure(R.string.ai_invalid_photo)
  val input = ARecipeInput(draft.recipeId, r.categoryId, r.title, r.description, r.instructions,
   r.prepMinutes, r.cookMinutes, r.servings, lines, draft.version,
   photoMode = if (photo == null) "Keep" else "Set", imageData = photo?.image)
  val body = ARecipeSave(draft.operationId, input, r.source, draft.sourceKind)
  mutation("/v1/recipes/save",WireJson.encodeToString(body),"recipe",draftId=draft.localId)
 }
 private fun AMeal.withWirePortions() = copy(
  portions=portionsText?.let { Quantities.input(it) } ?: Quantities.validate(portions),
  portionsText=null
 )

 private fun planInput():APlanApply{
  val w=workspace.value;val context=requireNotNull(w.proposalContext?:w.context);val meals=w.meals.map{m->
   val input = m.withWirePortions()
   if(m.draftIndex!=null) input.copy(recipeId=w.recipes.getOrNull(m.draftIndex)?.publishedId
    ?: throw RecipeReviewFailure(R.string.ai_review_recipes_first),draftIndex=null) else input
  }
  val versions=meals.map{requireNotNull(it.recipeId)}.distinct().associateWith{id->val version=w.recipes.lastOrNull{it.publishedId==id}?.version?:context.candidates.find{it.recipeId==id}?.version;val bytes=android.util.Base64.decode(requireNotNull(version),android.util.Base64.DEFAULT);"0x"+bytes.joinToString(""){"%02X".format(it)}}
  return APlanApply(w.planOperation,(w.planJobId ?: w.job?.takeIf{it.kind=="plan"}?.jobId),meals,w.replaceIds,versions,context.plan.associate{it.id to it.version},context.batches.associate{it.id to it.version})
 }
 suspend fun applyPlan(){
  val input = planInput()
  if (input != previewPayload) throw RecipeReviewFailure(R.string.ai_stale_preview)
  mutation("/v1/plans/apply",WireJson.encodeToString(input),"plan")
  context()
 }
 suspend fun previewPlan(){
  invalidatePreview()
  val input = planInput()
  val current = workspace.value
  val result = WireJson.decodeFromString<APreview>(api().call("POST","/v1/plans/preview",WireJson.encodeToString(input)))
  if (current == workspace.value) {
   previewPayload = input
   preview.value = result
  }
 }
 suspend fun acceptPurchases(){
  val w = workspace.value
  val job = requireNotNull(w.receiptJob ?: w.job?.takeIf { it.kind == "receipt" })
  val purchases = w.purchases.mapIndexed { index, purchase ->
   if (index !in w.selectedPurchases) return@mapIndexed purchase.copy(quantityText=null)
   require(purchase.ingredientId != null && purchase.unitId != null && purchase.purchasedOn != null)
   LocalDate.parse(purchase.purchasedOn)
   val quantity = purchase.quantityText?.let { Quantities.input(it) }
    ?: Quantities.validate(requireNotNull(purchase.quantity))
   purchase.copy(quantity=quantity,quantityText=null)
  }
  mutation("/v1/ai/jobs/${job.jobId}/purchases",WireJson.encodeToString(
   AReceiptApply(w.receiptOperation,job.version,w.selectedPurchases,purchases,confirmDuplicate=w.confirmedDuplicate)
  ),"receipt")
 }
 suspend fun createIngredient(recipeIndex:Int,lineIndex:Int){
  val draft = workspace.value.recipes[recipeIndex]
  val line = draft.recipe.ingredients[lineIndex]
  require(line.name.isNotBlank() && line.unitId != null && line.ingredientId == null)
  val body = buildJsonObject {
   put("operationId", UUID.randomUUID().toString())
   put("name", line.name)
   put("unitId", line.unitId)
  }.toString()
  mutation("/v1/ingredients", body, "ingredient", draftId=draft.localId, ingredientRowId=line.localId)
  context()
 }

 fun editRecipe(index:Int,change:(ARecipe)->ARecipe){edit{w->w.copy(recipes=w.recipes.mapIndexed{i,r->if(i==index)r.copy(recipe=change(r.recipe))else r})}}
}
