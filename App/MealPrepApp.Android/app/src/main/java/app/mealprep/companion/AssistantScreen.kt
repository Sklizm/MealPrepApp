package app.mealprep.companion

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.*
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.unit.*
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.core.content.FileProvider
import kotlinx.coroutines.*
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.UUID
import kotlin.math.*

@Composable fun MealWorkspaceScreen(graph:AppGraph,repository:CompanionRepository,settings:PreferencesState,initialTab:Int=0,onClose:(()->Unit)?=null){
 val controller=remember(repository){AssistantRepository(repository,graph)};val w by controller.workspace.collectAsState();val busy by controller.busy.collectAsState();val message by controller.message.collectAsState();val enabled by controller.capabilities.collectAsState();val consent by controller.consent.collectAsState();val jobs by controller.jobs.collectAsState();val browser by controller.browser.collectAsState();val preview by controller.preview.collectAsState()
 val scope=rememberCoroutineScope();var restored by remember{mutableStateOf(false)};var tab by rememberSaveable{mutableIntStateOf(initialTab)};var selectedRecipe by rememberSaveable{mutableIntStateOf(0)};var search by rememberSaveable{mutableStateOf("")}
 fun action(block:suspend()->Unit){scope.launch{controller.run(block)}}
 LaunchedEffect(repository){controller.restore();restored=true;controller.run{controller.refresh()}}
 LaunchedEffect(w,restored){if(restored){delay(300);controller.persist()}}
 Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),verticalArrangement=Arrangement.spacedBy(10.dp)){
  Row{Text(stringResource(R.string.ai_title),style=MaterialTheme.typography.headlineSmall,modifier=Modifier.weight(1f));if(onClose!=null)TextButton(onClick=onClose){Text(stringResource(R.string.close))}}
  Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)){listOf(R.string.ai_request,R.string.ai_recipes,R.string.ai_plan,R.string.ai_purchases,R.string.ai_suggestions,R.string.ai_recent).forEachIndexed{i,label->FilterChip(selected=tab==i,onClick={tab=i},label={Text(stringResource(label))})}}
  if(busy)LinearProgressIndicator(Modifier.fillMaxWidth())
  message?.let{Text(stringResource(it),color=MaterialTheme.colorScheme.primary)}
  if(w.pending!=null){Text(stringResource(R.string.ai_pending));Button(onClick={action{controller.recover()}},enabled=!busy){Text(stringResource(R.string.ai_recover_pending))}}
  when(tab){
   0->{
    Text(stringResource(R.string.ai_notice));Row{Checkbox(consent,{value->action{controller.setConsent(value)}});Text(stringResource(R.string.ai_accept_notice))}
    if(enabled?.enabled!=true)Text(stringResource(R.string.ai_disabled))
    APicker(stringResource(R.string.ai_task),listOf("suggest","import","generate","plan","recipe-photo","receipt"),w.kind,{kind->taskLabel(kind)},{kind->controller.edit{it.copy(kind=kind)}})
    AField(stringResource(R.string.ai_prompt),w.prompt,{text->controller.edit{it.copy(prompt=text.take(4000))}},multi=true)
    AField(stringResource(R.string.ai_source),w.source,{text->controller.edit{it.copy(source=text.take(200000))}},multi=true)
    ADate(stringResource(R.string.ai_week_start),w.startDate){date->controller.edit{it.copy(startDate=date?:LocalDate.now().toString())}}
    TextButton(onClick={action{controller.importPreview();selectedRecipe=controller.workspace.value.recipes.lastIndex;tab=1}},enabled=!busy){Text(stringResource(R.string.review_import))}
    PhotoInput(onUpload={bytes->action{controller.upload(bytes)}})
    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){Button(onClick={action{controller.generate(settings.language);tab=when(w.kind){"plan"->2;"receipt"->3;"suggest"->4;else->1}}},enabled=!busy&&enabled?.enabled==true&&consent){Text(stringResource(R.string.ai_generate))};TextButton(onClick={scope.launch{controller.cancel()}},enabled=w.job?.state in listOf("Queued","Running")){Text(stringResource(R.string.ai_cancel))}}
    w.job?.let{Text(jobStateLabel(it.state))}
    HorizontalDivider();Text(stringResource(R.string.ai_preferences),style=MaterialTheme.typography.titleMedium)
    AField(stringResource(R.string.ai_servings),w.preferences.servings,{text->controller.edit{it.copy(preferences=it.preferences.copy(servings=text))}},numeric=true)
    AField(stringResource(R.string.ai_max_minutes),w.preferences.maxMinutes.toString(),{text->text.toIntOrNull()?.let{v->controller.edit{it.copy(preferences=it.preferences.copy(maxMinutes=v.coerceIn(1,10080)))}}},numeric=true)
    AField(stringResource(R.string.ai_repetitions),w.preferences.maxRepetitions.toString(),{text->text.toIntOrNull()?.let{v->controller.edit{it.copy(preferences=it.preferences.copy(maxRepetitions=v.coerceIn(1,7)))}}},numeric=true)
    AToggle(stringResource(R.string.ai_expiring),w.preferences.preferExpiring){v->controller.edit{it.copy(preferences=it.preferences.copy(preferExpiring=v))}}
    AToggle(stringResource(R.string.ai_leftovers),w.preferences.useLeftovers){v->controller.edit{it.copy(preferences=it.preferences.copy(useLeftovers=v))}}
    AToggle(stringResource(R.string.ai_new_drafts),w.allowNewRecipes){v->controller.edit{it.copy(allowNewRecipes=v)}}
    Text(stringResource(R.string.ai_exclusions));var excludedSearch by rememberSaveable{mutableStateOf("")};AField(stringResource(R.string.search_ingredient),excludedSearch,{excludedSearch=it})
    w.context?.ingredients?.filter{it.name.contains(excludedSearch,true)}?.take(30)?.forEach{i->AToggle(i.name,i.ingredientId in w.preferences.excludedIngredientIds){value->controller.edit{state->state.copy(preferences=state.preferences.copy(excludedIngredientIds=if(value)state.preferences.excludedIngredientIds+i.ingredientId else state.preferences.excludedIngredientIds-i.ingredientId))}}}
    Text(stringResource(R.string.ai_slots));w.context?.categories?.forEach{slot->AToggle(slot.name,slot.categoryId in (w.preferences.mealSlotIds?:w.context!!.categories.map{it.categoryId})){value->controller.edit{state->val old=state.preferences.mealSlotIds?:state.context!!.categories.map{it.categoryId};state.copy(preferences=state.preferences.copy(mealSlotIds=if(value)old+slot.categoryId else old-slot.categoryId))}}}
    Button(onClick={action{controller.savePreferences()}},enabled=!busy){Text(stringResource(R.string.ai_save_preferences))}
   }
   1->{
    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){Button(onClick={controller.edit{it.copy(recipes=it.recipes+ARecipeDraft())};selectedRecipe=w.recipes.size}){Text(stringResource(R.string.ai_new_recipe))};TextButton(onClick={controller.edit{it.copy(kind="generate")};tab=0}){Text(stringResource(R.string.ai_generate))}}
    AField(stringResource(R.string.search_ingredient),search,{search=it})
    TextButton(onClick={action{controller.browse(search)}},enabled=!busy){Text(stringResource(R.string.ai_refresh))}
    (if(browser.isEmpty())w.context?.candidates?.map{ABrowseRecipe(it.recipeId,it.title)}?:emptyList() else browser).forEach{row->TextButton(onClick={action{controller.openRecipe(row.recipeId);selectedRecipe=controller.workspace.value.recipes.lastIndex}},enabled=!busy){Text(row.title)}}
    if(w.recipes.isNotEmpty())APicker(stringResource(R.string.ai_recipes),w.recipes.indices.toList(),selectedRecipe,{i->w.recipes[i].recipe.title.ifBlank{stringResource(R.string.ai_new_recipe)}},{selectedRecipe=it})
    w.recipes.getOrNull(selectedRecipe)?.let{draft->RecipeReview(draft.recipe,w.context,{change->controller.editRecipe(selectedRecipe,change)},{line->action{controller.createIngredient(selectedRecipe,line)}})
     Button(onClick={action{controller.saveRecipe(selectedRecipe)}},enabled=!busy){Text(stringResource(R.string.ai_save_recipe))}
    }
   }
   2->{
    Text(stringResource(R.string.ai_plan_hint));ADate(stringResource(R.string.ai_week_start),w.startDate){date->controller.edit{it.copy(startDate=date?:LocalDate.now().toString())}}
    Row{TextButton(onClick={action{controller.context()}},enabled=!busy){Text(stringResource(R.string.ai_refresh))};Button(onClick={controller.edit{it.copy(kind="plan")};tab=0}){Text(stringResource(R.string.ai_generate))}}
    Text(stringResource(R.string.ai_replace_meals));w.context?.plan?.forEach{entry->AToggle("${entry.plannedDate.take(10)} · ${entry.recipeTitle}",entry.id in w.replaceIds,enabled=!entry.isPrepared&&entry.consumedPortions.toBigDecimal().signum()==0){v->controller.edit{it.copy(replaceIds=if(v)it.replaceIds+entry.id else it.replaceIds-entry.id)}}}
    w.meals.forEachIndexed{index,meal->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
     fun change(transform:(AMeal)->AMeal)=controller.edit{it.copy(meals=it.meals.mapIndexed{i,m->if(i==index)transform(m)else m})}
     ADate(stringResource(R.string.ai_meal_date),meal.date){date->if(date!=null)change{it.copy(date=date)}}
     APicker(stringResource(R.string.ai_slots),w.context?.categories?:emptyList(),w.context?.categories?.find{it.categoryId==meal.slotId},{it.name},{slot->change{it.copy(slotId=slot.categoryId)}})
     APicker(stringResource(R.string.ai_recipes),w.context?.candidates?:emptyList(),w.context?.candidates?.find{it.recipeId==meal.recipeId},{it.title},{recipe->change{it.copy(recipeId=recipe.recipeId,draftIndex=null,preparedBatchId=null)}})
     meal.draftIndex?.let{i->Text(w.recipes.getOrNull(i)?.recipe?.title?:stringResource(R.string.ai_review))}
     AField(stringResource(R.string.ai_servings),meal.portions,{value->change{it.copy(portions=value)}},numeric=true)
     AField(stringResource(R.string.ai_batch),meal.batchKey,{value->change{it.copy(batchKey=value.take(80))}})
     Text(meal.reason);AToggle(stringResource(R.string.ai_pin),index in w.pinned){v->controller.edit{it.copy(pinned=if(v)it.pinned+index else it.pinned-index)}}
     TextButton(onClick={controller.edit{state->state.copy(meals=state.meals.filterIndexed{i,_->i!=index},pinned=emptyList())}}){Text(stringResource(R.string.ai_remove))}
    }}}
    Button(onClick={val slot=w.context?.categories?.firstOrNull()?.categoryId?:return@Button;val recipe=w.context?.candidates?.firstOrNull()?.recipeId?:return@Button;controller.edit{it.copy(meals=it.meals+AMeal(it.startDate,slot,recipe,portions=it.preferences.servings))}}){Text(stringResource(R.string.ai_add_meal))}
    TextButton(onClick={action{controller.previewPlan()}},enabled=!busy&&w.meals.isNotEmpty()){Text(stringResource(R.string.ai_preview_plan))}
    preview?.let{p->Text(stringResource(R.string.ai_plan_preview));Text(stringResource(R.string.ai_preview_totals,p.cookingBatches,p.cookingMinutes,Quantities.format(p.calories)));if(!p.nutritionComplete)Text(stringResource(R.string.ai_nutrition_incomplete));p.shopping.forEach{line->Text("${line.name}: ${Quantities.format(line.fromPantry)} / ${Quantities.format(line.missing)} ${line.unit}")}}
    Button(onClick={action{controller.applyPlan()}},enabled=!busy&&w.meals.isNotEmpty()){Text(stringResource(R.string.ai_apply_plan))}
   }
   3->{
    Text(stringResource(R.string.ai_receipt_hint));Button(onClick={controller.edit{it.copy(kind="receipt")};tab=0}){Text(stringResource(R.string.ai_photo))}
    w.purchases.forEachIndexed{index,purchase->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
     fun change(transform:(APurchase)->APurchase)=controller.edit{it.copy(purchases=it.purchases.mapIndexed{i,p->if(i==index)transform(p)else p})}
     AToggle(purchase.original,index in w.selectedPurchases,enabled=index !in w.acceptedPurchases){v->controller.edit{it.copy(selectedPurchases=if(v)it.selectedPurchases+index else it.selectedPurchases-index)}}
     AIngredientPicker(w.context,purchase.name,purchase.ingredientId,{id,name->change{it.copy(ingredientId=id,name=name)}})
     APicker(stringResource(R.string.purchase_unit),w.context?.units?:emptyList(),w.context?.units?.find{it.unitId==purchase.unitId},{it.abbreviation},{u->change{it.copy(unitId=u.unitId,unit=u.abbreviation)}})
     AField(stringResource(R.string.actual_purchase),purchase.quantity?:"",{value->change{it.copy(quantity=value)}},numeric=true)
     ADate(stringResource(R.string.ai_purchase_date),purchase.purchasedOn){date->change{it.copy(purchasedOn=date)}}
     ADate(stringResource(R.string.ai_expiry_date),purchase.expiresOn){date->change{it.copy(expiresOn=date)}}
    }}}
    AToggle(stringResource(R.string.ai_confirm_duplicate),w.confirmedDuplicate){v->controller.edit{it.copy(confirmedDuplicate=v)}}
    Button(onClick={action{controller.acceptPurchases()}},enabled=!busy&&w.selectedPurchases.isNotEmpty()){Text(stringResource(R.string.ai_accept_purchases))}
   }
   4->{
    Button(onClick={controller.edit{it.copy(kind="suggest")};tab=0}){Text(stringResource(R.string.ai_generate))};Text(stringResource(R.string.ai_readiness_hint))
    w.suggestions.forEach{s->val candidate=w.context?.candidates?.find{it.recipeId==s.recipeId};Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(6.dp)){
     Text(candidate?.title?:"",style=MaterialTheme.typography.titleMedium);Text(s.reason)
     candidate?.let{c->Text(stringResource(when(c.readiness.status){0->R.string.ai_ready;1->R.string.ai_missing;else->R.string.ai_review}));Text(stringResource(R.string.ai_time_servings,c.prepMinutes?:0,c.cookMinutes?:0,c.servings?:0));c.readiness.ingredients.filter{it.missing.toBigDecimal().signum()>0||it.needsReview}.forEach{line->Text("${line.name}: ${Quantities.format(line.missing)}")}}
     TextButton(onClick={action{controller.openRecipe(s.recipeId);selectedRecipe=controller.workspace.value.recipes.lastIndex;tab=1}},enabled=!busy){Text(stringResource(R.string.ai_open_recipe))}
    }}}
    if(w.suggestions.isEmpty())w.context?.candidates?.take(10)?.forEach{c->Text(c.title);TextButton(onClick={action{controller.openRecipe(c.recipeId);selectedRecipe=controller.workspace.value.recipes.lastIndex;tab=1}}){Text(stringResource(R.string.ai_open_recipe))}}
   }
   5->{
    TextButton(onClick={action{controller.recent()}},enabled=!busy){Text(stringResource(R.string.ai_refresh))}
    jobs.forEach{job->Card{Column(Modifier.padding(12.dp)){Text(taskLabel(job.kind));Text(jobStateLabel(job.state));Text(job.createdAt.take(19));TextButton(onClick={action{controller.poll(job.jobId);tab=when(job.kind){"suggest"->4;"plan"->2;"receipt"->3;else->1}}},enabled=!busy){Text(stringResource(R.string.ai_open_result))}}}}
   }
  }
  w.warnings.forEach{Text(it)}
  Spacer(Modifier.height(24.dp))
 }
}

@Composable private fun AField(label:String,value:String,onChange:(String)->Unit,numeric:Boolean=false,multi:Boolean=false){OutlinedTextField(value,onChange,label={Text(label)},modifier=Modifier.fillMaxWidth(),singleLine=!multi,minLines=if(multi)3 else 1,keyboardOptions=KeyboardOptions(keyboardType=if(numeric)KeyboardType.Decimal else KeyboardType.Text))}
@Composable private fun AToggle(label:String,value:Boolean,enabled:Boolean=true,onChange:(Boolean)->Unit){Row{Checkbox(value,onChange,enabled=enabled);Text(label,Modifier.weight(1f).padding(top=10.dp))}}
@Composable private fun<T> APicker(label:String,values:List<T>,selected:T?,display: @Composable (T)->String,onChoose:(T)->Unit){var open by remember{mutableStateOf(false)};Column{Text(label,style=MaterialTheme.typography.labelMedium);Box{OutlinedButton(onClick={open=true},modifier=Modifier.fillMaxWidth()){Text(selected?.let{display(it)}?:stringResource(R.string.ai_choose))};DropdownMenu(open,{open=false},modifier=Modifier.heightIn(max=320.dp)){values.forEach{v->DropdownMenuItem(text={Text(display(v))},onClick={onChoose(v);open=false})}}}}}
@Composable private fun AIngredientPicker(context:AContext?,name:String,id:Int?,onChoose:(Int,String)->Unit){var search by remember(id){mutableStateOf(name)};AField(stringResource(R.string.search_ingredient),search,{search=it});APicker(stringResource(R.string.optional_ingredient),context?.ingredients?.filter{it.name.contains(search,true)}?.take(50)?:emptyList(),context?.ingredients?.find{it.ingredientId==id},{it.name},{onChoose(it.ingredientId,it.name)})}
@Composable private fun RecipeReview(recipe:ARecipe,context:AContext?,edit:((ARecipe)->ARecipe)->Unit,createIngredient:(Int)->Unit){
 Text(stringResource(R.string.ai_review_recipe));AField(stringResource(R.string.ai_recipe_title),recipe.title,{value->edit{it.copy(title=value.take(150))}});AField(stringResource(R.string.ai_description),recipe.description,{value->edit{it.copy(description=value)}},multi=true)
 APicker(stringResource(R.string.ai_slots),context?.categories?:emptyList(),context?.categories?.find{it.categoryId==recipe.categoryId},{it.name},{c->edit{it.copy(categoryId=c.categoryId)}})
 AField(stringResource(R.string.ai_servings),recipe.servings?.toString()?:"",{text->edit{it.copy(servings=text.toIntOrNull())}},numeric=true)
 AField(stringResource(R.string.ai_prep),recipe.prepMinutes?.toString()?:"",{text->edit{it.copy(prepMinutes=text.toIntOrNull())}},numeric=true)
 AField(stringResource(R.string.ai_cook),recipe.cookMinutes?.toString()?:"",{text->edit{it.copy(cookMinutes=text.toIntOrNull())}},numeric=true)
 recipe.ingredients.forEachIndexed{index,line->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
  fun change(transform:(ALine)->ALine)=edit{it.copy(ingredients=it.ingredients.mapIndexed{i,l->if(i==index)transform(l)else l})}
  Text(line.original);AField(stringResource(R.string.ai_ingredient_name),line.name,{value->change{it.copy(name=value)}})
  AIngredientPicker(context,line.name,line.ingredientId,{id,name->change{it.copy(ingredientId=id,name=name)}})
  APicker(stringResource(R.string.purchase_unit),context?.units?:emptyList(),context?.units?.find{it.unitId==line.unitId},{it.abbreviation},{u->change{it.copy(unitId=u.unitId,unit=u.abbreviation)}})
  AField(stringResource(R.string.quantity),line.quantity?:"",{value->change{it.copy(quantity=value)}},numeric=true);AField(stringResource(R.string.ai_notes),line.notes?:"",{value->change{it.copy(notes=value)}})
  Row{TextButton(onClick={createIngredient(index)}){Text(stringResource(R.string.ai_create_ingredient))};TextButton(onClick={edit{it.copy(ingredients=it.ingredients.filterIndexed{i,_->i!=index})}}){Text(stringResource(R.string.ai_remove))}}
 }}}
 TextButton(onClick={edit{it.copy(ingredients=it.ingredients+ALine())}}){Text(stringResource(R.string.ai_add_ingredient))}
 AField(stringResource(R.string.ai_instructions),recipe.instructions,{value->edit{it.copy(instructions=value)}},multi=true);recipe.source?.let{Text(it)}
}
@Composable private fun taskLabel(kind:String)=stringResource(when(kind){"suggest"->R.string.ai_task_suggest;"import"->R.string.ai_task_import;"generate"->R.string.ai_task_generate;"plan"->R.string.ai_task_plan;"receipt"->R.string.ai_task_receipt;else->R.string.ai_task_recipe_photo})
@Composable private fun jobStateLabel(state:String)=stringResource(when(state){"Queued","Running"->R.string.ai_waiting;"Completed"->R.string.ai_review;"Cancelled"->R.string.ai_cancelled;else->R.string.ai_unavailable})
@OptIn(ExperimentalMaterial3Api::class)
@Composable private fun ADate(label:String,value:String?,onChange:(String?)->Unit){var open by remember{mutableStateOf(false)};Row{OutlinedButton(onClick={open=true},modifier=Modifier.weight(1f)){Text(label+": "+(value?.let{runCatching{LocalDate.parse(it).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM))}.getOrNull()}?:stringResource(R.string.unknown_date)))};TextButton(onClick={onChange(null)}){Text(stringResource(R.string.clear))}};if(open){val state=rememberDatePickerState(initialSelectedDateMillis=value?.let{runCatching{LocalDate.parse(it).atStartOfDay(java.time.ZoneOffset.UTC).toInstant().toEpochMilli()}.getOrNull()});DatePickerDialog(onDismissRequest={open=false},confirmButton={TextButton(onClick={state.selectedDateMillis?.let{onChange(java.time.Instant.ofEpochMilli(it).atZone(java.time.ZoneOffset.UTC).toLocalDate().toString())};open=false}){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={open=false}){Text(stringResource(R.string.cancel))}}){DatePicker(state)}}}

@Composable private fun PhotoInput(onUpload:(ByteArray)->Unit){
 val context=LocalContext.current;var image by remember{mutableStateOf<Bitmap?>(null)};var error by remember{mutableStateOf(false)};val cameraFile=remember{java.io.File(java.io.File(context.cacheDir,"ai-camera").apply{mkdirs()},UUID.randomUUID().toString()+".jpg")}
 fun decode(uri:Uri){try{val info=BitmapFactory.Options().apply{inJustDecodeBounds=true};context.contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,info)};require(info.outWidth>0&&info.outHeight>0);val options=BitmapFactory.Options().apply{inSampleSize=max(1,max(info.outWidth,info.outHeight)/2400)};image=context.contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,options)};require(image!=null)}catch(e:Exception){error=true}}
 val select=rememberLauncherForActivityResult(ActivityResultContracts.GetContent()){uri->uri?.let{decode(it)}}
 val camera=rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()){ok->if(ok)decode(FileProvider.getUriForFile(context,context.packageName+".photos",cameraFile));cameraFile.delete()}
 Row{TextButton(onClick={select.launch("image/*")}){Text(stringResource(R.string.ai_photo))};TextButton(onClick={camera.launch(FileProvider.getUriForFile(context,context.packageName+".photos",cameraFile))}){Text(stringResource(R.string.ai_camera))}}
 if(error)Text(stringResource(R.string.ai_unavailable));image?.let{bitmap->PhotoReview(bitmap,onDismiss={image=null},onAccept={bytes->image=null;onUpload(bytes)})}
 DisposableEffect(Unit){onDispose{cameraFile.delete()}}
}
@Composable private fun PhotoReview(original:Bitmap,onDismiss:()->Unit,onAccept:(ByteArray)->Unit){
 var bitmap by remember{mutableStateOf(original.copy(Bitmap.Config.ARGB_8888,true))};var selection by remember{mutableStateOf<Rect?>(null)};var origin by remember{mutableStateOf(Offset.Zero)};var canvasSize by remember{mutableStateOf(IntSize(1,1))}
 Dialog(onDismissRequest=onDismiss,properties=DialogProperties(usePlatformDefaultWidth=false)){Surface(Modifier.fillMaxWidth().padding(12.dp)){Column(Modifier.verticalScroll(rememberScrollState()).padding(16.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
  Text(stringResource(R.string.ai_image_hint));Canvas(Modifier.fillMaxWidth().height(320.dp).pointerInput(bitmap){detectDragGestures(onDragStart={origin=it;selection=Rect(it,it)},onDrag={change,_->selection=Rect(min(origin.x,change.position.x),min(origin.y,change.position.y),max(origin.x,change.position.x),max(origin.y,change.position.y));change.consume()})}){canvasSize=IntSize(size.width.toInt(),size.height.toInt());drawImage(bitmap.asImageBitmap(),dstSize=canvasSize);selection?.let{drawRect(androidx.compose.ui.graphics.Color.Black.copy(alpha=.5f),it.topLeft,it.size)}}
  fun selected():android.graphics.Rect?{val rect=selection?:return null;val sx=bitmap.width.toFloat()/canvasSize.width;val sy=bitmap.height.toFloat()/canvasSize.height;return android.graphics.Rect((rect.left*sx).toInt().coerceIn(0,bitmap.width),(rect.top*sy).toInt().coerceIn(0,bitmap.height),(rect.right*sx).toInt().coerceIn(0,bitmap.width),(rect.bottom*sy).toInt().coerceIn(0,bitmap.height)).takeIf{it.width()>1&&it.height()>1}}
  Row{TextButton(onClick={selected()?.let{r->bitmap=Bitmap.createBitmap(bitmap,r.left,r.top,r.width(),r.height());selection=null}}){Text(stringResource(R.string.ai_crop))};TextButton(onClick={selected()?.let{r->val next=bitmap.copy(Bitmap.Config.ARGB_8888,true);android.graphics.Canvas(next).drawRect(r,android.graphics.Paint().apply{color=android.graphics.Color.BLACK;style=android.graphics.Paint.Style.FILL});bitmap=next;selection=null}}){Text(stringResource(R.string.ai_redact))};TextButton(onClick={bitmap=Bitmap.createBitmap(bitmap,0,0,bitmap.width,bitmap.height,Matrix().apply{postRotate(90f)},true);selection=null}){Text(stringResource(R.string.ai_rotate))}}
  Button(onClick={val output=java.io.ByteArrayOutputStream();bitmap.compress(Bitmap.CompressFormat.JPEG,88,output);onAccept(output.toByteArray())}){Text(stringResource(R.string.ai_upload_reviewed))};TextButton(onClick=onDismiss){Text(stringResource(R.string.cancel))}
 }}}
}
