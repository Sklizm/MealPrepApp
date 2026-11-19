package app.mealprep.companion

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.compose.foundation.relocation.BringIntoViewRequester
import androidx.compose.foundation.relocation.bringIntoViewRequester
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
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
import java.util.UUID
import kotlin.math.*

@OptIn(ExperimentalLayoutApi::class)
@Composable fun MealWorkspaceScreen(controller: AssistantRepository, settings:PreferencesState, initialTab:Int=0, onClose:(()->Unit)?=null, onSignIn: () -> Unit = {}, actionScope: CoroutineScope = rememberCoroutineScope()){
 val w by controller.workspace.collectAsState()
 val busy by controller.busy.collectAsState()
 val message by controller.message.collectAsState()
 val enabled by controller.capabilities.collectAsState()
 val consent by controller.consent.collectAsState()
 val jobs by controller.jobs.collectAsState()
 val browser by controller.browser.collectAsState()
 val preview by controller.preview.collectAsState()
 val opened by controller.openedDraft.collectAsState()
 val destination by controller.resultTab.collectAsState()
 val scope=rememberCoroutineScope()
 var tab by rememberSaveable { mutableIntStateOf(initialTab) }
 var selectedId by rememberSaveable { mutableStateOf<String?>(null) }
 var search by rememberSaveable { mutableStateOf("") }
 var lastOpenedRecipe by rememberSaveable { mutableStateOf<Int?>(null) }
 var restoreBrowseFocus by rememberSaveable { mutableStateOf(false) }
 val browseScroll = rememberScrollState()
 val workScroll = rememberScrollState()
 val requestScroll = rememberScrollState()
 val keyboard = LocalSoftwareKeyboardController.current

 fun action(block:suspend()->Unit){actionScope.launch{controller.run(block)}}
 LaunchedEffect(opened, destination) {
  destination?.let { tab = it; controller.resultTab.value = null }
  opened?.let { selectedId = it; tab = 1; controller.openedDraft.value = null }
 }
 LaunchedEffect(tab, search) {
  if (tab == 1) { delay(250); controller.browse(search) }
 }
 val selectedIndex = w.recipes.indexOfFirst { it.localId == selectedId }
 if (tab == 1 && selectedIndex >= 0) {
  val draft = w.recipes[selectedIndex]
  fun goBack() { selectedId = null; restoreBrowseFocus = true; keyboard?.hide() }
  BackHandler(onBack=::goBack)
  key(draft.localId) {
   RecipeReviewScreen(
    draft, w.context, message, busy, w.pending != null,
    onBack = ::goBack,
    photo = { controller.recipePhoto(draft.photoKey) },
    skipPhoto = { action { controller.skipRecipePhoto(selectedIndex) } },
    edit = { change -> controller.editRecipe(selectedIndex, change) },
    createIngredient = { row -> action { controller.createIngredient(selectedIndex, row) } },
    save = { action { controller.saveRecipe(selectedIndex) } },
    recover = { action { controller.recover() } },
    signIn = onSignIn)
  }
  return
 }
 val scroll = when (tab) { 0 -> requestScroll; 1 -> browseScroll; else -> workScroll }
 Column(Modifier.fillMaxSize().verticalScroll(scroll).padding(16.dp),verticalArrangement=Arrangement.spacedBy(10.dp)){
  if(onClose!=null) TextButton(onClick=onClose){Text(stringResource(R.string.close))}
  Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)){listOf(R.string.ai_request,R.string.ai_recipes,R.string.ai_plan,R.string.ai_purchases,R.string.ai_suggestions,R.string.ai_recent).forEachIndexed{i,label->FilterChip(selected=tab==i,onClick={if(tab != i) { tab=i; scope.launch { workScroll.scrollTo(0) } }},label={Text(stringResource(label))})}}
  if(busy)LinearProgressIndicator(Modifier.fillMaxWidth())
  AssistantFeedback(message, w.pending != null, busy, { action { controller.recover() } }, onSignIn)
  when(tab){
   0->{
    APicker(stringResource(R.string.ai_task),listOf("suggest","import","generate","plan","recipe-photo","receipt"),w.kind,
     {kind->taskLabel(kind)}, {kind->controller.edit{it.copy(kind=kind)}})
    if (w.kind == "import") {
     AField(stringResource(R.string.ai_source),w.source,{text->controller.edit{it.copy(source=text.take(200000))}},multi=true)
     Button(onClick={action{controller.importPreview()}},enabled=!busy&&w.source.isNotBlank()){Text(stringResource(R.string.review_import))}
    } else {
     if (!consent) {
      Text(stringResource(R.string.ai_notice))
      AToggle(stringResource(R.string.ai_accept_notice), consent, enabled = !busy) { value -> action { controller.setConsent(value) } }
     }
     if(enabled?.enabled!=true) Text(stringResource(R.string.ai_disabled))
     val needsImage = w.kind in setOf("recipe-photo", "receipt")
     if (needsImage) {
      if (consent) PhotoInput(onUpload={bytes->action{controller.upload(bytes)}})
      Text(stringResource(if (w.uploadKind == w.kind) R.string.ai_image_ready else R.string.ai_image_required))
     }
     if (w.kind != "receipt") AField(stringResource(R.string.ai_prompt),w.prompt,{text->controller.edit{it.copy(prompt=text.take(4000))}},multi=true)
     if (w.kind == "plan") ADate(stringResource(R.string.ai_week_start),w.startDate){date->controller.edit{it.copy(startDate=date?:LocalDate.now().toString())}}
     if (w.kind == "plan") {
      Text(stringResource(R.string.ai_slots))
      w.context?.categories?.forEach{slot->AToggle(slot.name,slot.categoryId in (w.preferences.mealSlotIds?:w.context!!.categories.map{it.categoryId})){value->controller.edit{state->val old=state.preferences.mealSlotIds?:state.context!!.categories.map{it.categoryId};state.copy(preferences=state.preferences.copy(mealSlotIds=if(value)old+slot.categoryId else old-slot.categoryId))}}}
     }
     Button(onClick={action{controller.generate(settings.language)}},
      enabled=!busy&&w.pending==null&&enabled?.enabled==true&&consent&&(!needsImage||w.uploadKind==w.kind)){
      Text(taskLabel(w.kind))
     }
    }
    if (w.job?.state in listOf("Queued","Running")) {
     Text(jobStateLabel(w.job!!.state))
     TextButton(onClick={scope.launch{controller.cancel()}}){Text(stringResource(R.string.ai_cancel))}
    }
    if (w.kind in setOf("suggest", "generate", "plan")) {
    HorizontalDivider();Text(stringResource(R.string.ai_preferences),style=MaterialTheme.typography.titleMedium)
    AField(stringResource(R.string.ai_servings),Quantities.editor(w.preferences.servings),{text->controller.edit{it.copy(preferences=it.preferences.copy(servings=text.replace(java.text.DecimalFormatSymbols(Quantities.region()).decimalSeparator, '.')))}},numeric=true)
    AField(stringResource(R.string.ai_max_minutes),w.preferences.maxMinutes.toString(),{text->text.toIntOrNull()?.let{v->controller.edit{it.copy(preferences=it.preferences.copy(maxMinutes=v.coerceIn(1,10080)))}}},numeric=true)
    AField(stringResource(R.string.ai_repetitions),w.preferences.maxRepetitions.toString(),{text->text.toIntOrNull()?.let{v->controller.edit{it.copy(preferences=it.preferences.copy(maxRepetitions=v.coerceIn(1,7)))}}},numeric=true)
    AToggle(stringResource(R.string.ai_expiring),w.preferences.preferExpiring){v->controller.edit{it.copy(preferences=it.preferences.copy(preferExpiring=v))}}
    AToggle(stringResource(R.string.ai_leftovers),w.preferences.useLeftovers){v->controller.edit{it.copy(preferences=it.preferences.copy(useLeftovers=v))}}
    if (w.kind == "plan") AToggle(stringResource(R.string.ai_new_drafts),w.allowNewRecipes){v->controller.edit{it.copy(allowNewRecipes=v)}}
    Text(stringResource(R.string.ai_exclusions));var excludedSearch by rememberSaveable{mutableStateOf("")};AField(stringResource(R.string.search_ingredient),excludedSearch,{excludedSearch=it})
    w.context?.ingredients?.filter{it.name.contains(excludedSearch,true)}?.take(30)?.forEach{i->AToggle(i.name,i.ingredientId in w.preferences.excludedIngredientIds){value->controller.edit{state->state.copy(preferences=state.preferences.copy(excludedIngredientIds=if(value)state.preferences.excludedIngredientIds+i.ingredientId else state.preferences.excludedIngredientIds-i.ingredientId))}}}
    TextButton(onClick={action{controller.savePreferences()}},enabled=!busy){Text(stringResource(R.string.ai_save_preferences))}
    }
   }
   1->{
    FlowRow(horizontalArrangement=Arrangement.spacedBy(8.dp)){
     Button(onClick={
      val draft = ARecipeDraft()
      controller.edit{it.copy(recipes=it.recipes+draft)}
      selectedId=draft.localId
     }){Text(stringResource(R.string.ai_new_recipe))}
     TextButton(onClick={controller.edit{it.copy(kind="import")};tab=0}){Text(stringResource(R.string.review_import))}
    }
    val drafts = w.recipes.filter { it.recipeId == null || it.canEdit }
    if(drafts.isNotEmpty()) APicker(stringResource(R.string.ai_drafts),drafts,null,
     {draft->draft.recipe.title.ifBlank{stringResource(R.string.ai_new_recipe)}},{selectedId=it.localId})
    AField(stringResource(R.string.search_recipes),search,{ value ->
     if (search != value) {
      search=value
      lastOpenedRecipe=null
      scope.launch { browseScroll.scrollTo(0) }
     }
    })
    TextButton(onClick={scope.launch { controller.browse(search) }},enabled=browser.status != RecipeSearchStatus.Loading){Text(stringResource(R.string.ai_refresh))}
    if (browser.query == search.trim()) {
     when (browser.status) {
      RecipeSearchStatus.Loading -> Text(stringResource(R.string.recipe_search_loading))
      RecipeSearchStatus.Failed -> Text(stringResource(R.string.recipe_search_failed))
      RecipeSearchStatus.Cached -> Text(stringResource(if (browser.rows.isEmpty()) R.string.recipe_search_cached_empty else R.string.recipe_search_cached))
      RecipeSearchStatus.Current -> if (browser.rows.isEmpty()) Text(stringResource(R.string.recipe_search_empty))
     }
     browser.rows.forEach { row -> key(row.recipeId) {
      val focus = remember { FocusRequester() }
      LaunchedEffect(lastOpenedRecipe, restoreBrowseFocus, busy) {
       if (restoreBrowseFocus && !busy && lastOpenedRecipe == row.recipeId) {
        focus.requestFocus()
        restoreBrowseFocus = false
       }
      }
      TextButton(onClick={action{controller.openRecipe(row.recipeId);lastOpenedRecipe=row.recipeId}},
       enabled=!busy,modifier=Modifier.focusRequester(focus)){Text(row.title)}
     }}
    } else Text(stringResource(R.string.recipe_search_loading))
   }
   2->{
    Text(stringResource(R.string.ai_plan_hint));ADate(stringResource(R.string.ai_week_start),w.startDate){date->controller.edit{it.copy(startDate=date?:LocalDate.now().toString())}}
    Row{TextButton(onClick={action{controller.context(refreshPlan=true)}},enabled=!busy){Text(stringResource(R.string.ai_refresh))};Button(onClick={controller.edit{it.copy(kind="plan")};tab=0}){Text(stringResource(R.string.ai_generate))}}
    Text(stringResource(R.string.ai_replace_meals));w.context?.plan?.forEach{entry->AToggle("${Quantities.date(entry.plannedDate)} · ${entry.recipeTitle}",entry.id in w.replaceIds,enabled=!entry.isPrepared&&entry.consumedPortions.toBigDecimal().signum()==0){v->controller.edit{it.copy(replaceIds=if(v)it.replaceIds+entry.id else it.replaceIds-entry.id)}}}
    w.meals.forEachIndexed{index,meal->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
     fun change(transform:(AMeal)->AMeal)=controller.edit{it.copy(meals=it.meals.mapIndexed{i,m->if(i==index)transform(m)else m})}
     ADate(stringResource(R.string.ai_meal_date),meal.date){date->if(date!=null)change{it.copy(date=date)}}
     APicker(stringResource(R.string.ai_slots),w.context?.categories?:emptyList(),w.context?.categories?.find{it.categoryId==meal.slotId},{it.name},{slot->change{it.copy(slotId=slot.categoryId)}})
     APicker(stringResource(R.string.ai_recipes),w.context?.candidates?:emptyList(),w.context?.candidates?.find{it.recipeId==meal.recipeId},{it.title},{recipe->change{it.copy(recipeId=recipe.recipeId,draftIndex=null,preparedBatchId=null)}})
     meal.draftIndex?.let{i->Text(w.recipes.getOrNull(i)?.recipe?.title?:stringResource(R.string.ai_review))}
     AField(stringResource(R.string.ai_servings),meal.portionsText ?: Quantities.editor(meal.portions),{value->change{it.copy(portionsText=value)}},numeric=true,problem=R.string.ai_quantity_field.takeIf { meal.portionsText?.let { runCatching { Quantities.input(it) }.isFailure } == true })
     AField(stringResource(R.string.ai_batch),meal.batchKey,{value->change{it.copy(batchKey=value.take(80))}})
     Text(meal.reason);AToggle(stringResource(R.string.ai_pin),index in w.pinned){v->controller.edit{it.copy(pinned=if(v)it.pinned+index else it.pinned-index)}}
     TextButton(onClick={controller.edit{state->state.copy(meals=state.meals.filterIndexed{i,_->i!=index},pinned=emptyList())}}){Text(stringResource(R.string.ai_remove))}
    }}}
    Button(onClick={val slot=w.context?.categories?.firstOrNull()?.categoryId?:return@Button;val recipe=w.context?.candidates?.firstOrNull()?.recipeId?:return@Button;controller.edit{it.copy(meals=it.meals+AMeal(it.startDate,slot,recipe,portions=it.preferences.servings))}}){Text(stringResource(R.string.ai_add_meal))}
    TextButton(onClick={action{controller.previewPlan()}},enabled=!busy&&w.pending==null&&w.meals.isNotEmpty()){Text(stringResource(R.string.ai_preview_plan))}
    preview?.let { plan ->
     Text(stringResource(R.string.ai_plan_preview))
     val calories = if (!plan.nutritionComplete && plan.calories.toBigDecimal().signum() == 0)
      stringResource(R.string.unknown_expiry) else Quantities.format(plan.calories, calories=true)
     Text(stringResource(R.string.ai_preview_totals, plan.cookingBatches, plan.cookingMinutes, calories))
     if(!plan.nutritionComplete) Text(stringResource(R.string.ai_nutrition_incomplete))
     if(plan.shopping.isNotEmpty()) {
      ShoppingPreviewColumns {
       Text(stringResource(R.string.ai_required), Modifier.weight(1f))
       Text(stringResource(R.string.ai_from_pantry), Modifier.weight(1f))
       Text(stringResource(R.string.ai_amount_missing), Modifier.weight(1f))
      }
      plan.shopping.forEach { line ->
       Text("${line.name} (${line.unit})", style=MaterialTheme.typography.titleSmall)
       ShoppingPreviewColumns {
        Text(Quantities.format(line.required), Modifier.weight(1f))
        Text(Quantities.format(line.fromPantry), Modifier.weight(1f))
        Text(Quantities.format(line.missing), Modifier.weight(1f))
       }
      }
     }
    }
    if (w.meals.isNotEmpty() && preview == null) Text(stringResource(R.string.ai_stale_preview))
    Button(onClick={action{controller.applyPlan()}},enabled=!busy&&preview!=null&&w.pending==null){Text(stringResource(R.string.ai_apply_plan))}

   }
   3->{
    Text(stringResource(R.string.ai_receipt_hint));Button(onClick={controller.edit{it.copy(kind="receipt")};tab=0}){Text(stringResource(R.string.ai_photo))}
    w.purchases.forEachIndexed{index,purchase->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
     fun change(transform:(APurchase)->APurchase)=controller.edit{it.copy(purchases=it.purchases.mapIndexed{i,p->if(i==index)transform(p)else p})}
     AToggle(purchase.original,index in w.selectedPurchases,enabled=index !in w.acceptedPurchases){v->controller.edit{it.copy(selectedPurchases=if(v)it.selectedPurchases+index else it.selectedPurchases-index)}}
     AIngredientPicker(w.context,purchase.name,purchase.ingredientId,{id,name->change{it.copy(ingredientId=id,name=name)}})
     APicker(stringResource(R.string.purchase_unit),w.context?.units?:emptyList(),w.context?.units?.find{it.unitId==purchase.unitId},{it.abbreviation},{u->change{it.copy(unitId=u.unitId,unit=u.abbreviation)}})
     AField(stringResource(R.string.actual_purchase),purchase.quantityText ?: Quantities.editor(purchase.quantity),{value->change{it.copy(quantityText=value)}},numeric=true,problem=if(purchase.quantityText?.let { runCatching { Quantities.input(it) }.isFailure } == true) R.string.ai_quantity_field else null)
     ADate(stringResource(R.string.ai_purchase_date),purchase.purchasedOn){date->change{it.copy(purchasedOn=date)}}
     ADate(stringResource(R.string.ai_expiry_date),purchase.expiresOn){date->change{it.copy(expiresOn=date)}}
    }}}
    AToggle(stringResource(R.string.ai_confirm_duplicate),w.confirmedDuplicate){v->controller.edit{it.copy(confirmedDuplicate=v)}}
    Button(onClick={action{controller.acceptPurchases()}},enabled=!busy&&w.pending==null&&w.selectedPurchases.isNotEmpty()){Text(stringResource(R.string.ai_accept_purchases))}
   }
   4->{
    Button(onClick={controller.edit{it.copy(kind="suggest")};tab=0}){Text(stringResource(R.string.ai_generate))};Text(stringResource(R.string.ai_readiness_hint))
    w.suggestions.forEach{s->val candidate=w.context?.candidates?.find{it.recipeId==s.recipeId};Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(6.dp)){
     Text(candidate?.title?:"",style=MaterialTheme.typography.titleMedium);Text(s.reason)
     candidate?.let{c->Text(stringResource(when(c.readiness.status){0->R.string.ai_ready;1->R.string.ai_missing;else->R.string.ai_review}));Text(recipeFacts(c.prepMinutes,c.cookMinutes,c.servings));c.readiness.ingredients.filter{it.missing.toBigDecimal().signum()>0||it.needsReview}.forEach{line->Text("${line.name}: ${Quantities.format(line.missing)}")}}
     TextButton(onClick={action{controller.openRecipe(s.recipeId)}},enabled=!busy){Text(stringResource(R.string.ai_open_recipe))}
    }}}
    if(w.suggestions.isEmpty())w.context?.candidates?.take(10)?.forEach{c->Text(c.title);TextButton(onClick={action{controller.openRecipe(c.recipeId)}}){Text(stringResource(R.string.ai_open_recipe))}}
   }
   5->{
    TextButton(onClick={action{controller.recent()}},enabled=!busy){Text(stringResource(R.string.ai_refresh))}
    jobs.forEach{job->Card{Column(Modifier.padding(12.dp)){Text(taskLabel(job.kind));Text(jobStateLabel(job.state));Text(Quantities.event(job.createdAt) ?: stringResource(R.string.unknown_date));TextButton(onClick={action{controller.poll(job.jobId)}},enabled=!busy){Text(stringResource(R.string.ai_open_result))}}}}
   }
  }
  w.warnings.forEach{Text(it)}
  Spacer(Modifier.height(24.dp))
 }
}

@Composable
private fun AField(label: String, value: String, onChange: (String) -> Unit, numeric: Boolean = false,
 multi: Boolean = false, problem: Int? = null, modifier: Modifier = Modifier) {
 OutlinedTextField(value,onChange,label={Text(label)},modifier=modifier.fillMaxWidth(),singleLine=!multi,
  minLines=if(multi)3 else 1,isError=problem!=null,
  supportingText=problem?.let { { Text(stringResource(it)) } },
  keyboardOptions=KeyboardOptions(keyboardType=if(numeric)KeyboardType.Decimal else KeyboardType.Text))
}
@Composable private fun AToggle(label:String,value:Boolean,enabled:Boolean=true,onChange:(Boolean)->Unit){Row{Checkbox(value,onChange,enabled=enabled);Text(label,Modifier.weight(1f).padding(top=10.dp))}}
@Composable private fun<T> APicker(label:String,values:List<T>,selected:T?,display: @Composable (T)->String,onChoose:(T)->Unit){var open by remember{mutableStateOf(false)};Column{Text(label,style=MaterialTheme.typography.labelMedium);Box{OutlinedButton(onClick={open=true},modifier=Modifier.fillMaxWidth()){Text(selected?.let{display(it)}?:stringResource(R.string.ai_choose))};DropdownMenu(open,{open=false},modifier=Modifier.heightIn(max=320.dp)){values.forEach{v->DropdownMenuItem(text={Text(display(v))},onClick={onChoose(v);open=false})}}}}}
@Composable private fun AIngredientPicker(context:AContext?,name:String,id:Int?,onChoose:(Int,String)->Unit){var search by remember(id){mutableStateOf(name)};AField(stringResource(R.string.search_ingredient),search,{search=it});APicker(stringResource(R.string.optional_ingredient),context?.ingredients?.filter{it.name.contains(search,true)}?.take(50)?:emptyList(),context?.ingredients?.find{it.ingredientId==id},{it.name},{onChoose(it.ingredientId,it.name)})}
@Composable
private fun AssistantFeedback(message: Int?, pending: Boolean, busy: Boolean, recover: () -> Unit, signIn: () -> Unit) {
 message?.takeUnless { it == R.string.ai_pending && pending }?.let { Text(stringResource(it)) }
 if (message == R.string.session_expired) TextButton(onClick=signIn) { Text(stringResource(R.string.sign_in_again)) }
 if (pending) {
  Text(stringResource(R.string.ai_pending))
  TextButton(onClick=recover, enabled=!busy) { Text(stringResource(R.string.ai_recover_pending)) }
 }
}

@Composable
private fun ShoppingPreviewColumns(content: @Composable RowScope.() -> Unit) {
 Row(Modifier.fillMaxWidth(), horizontalArrangement=Arrangement.spacedBy(8.dp), content=content)
}

@Composable
private fun recipeFacts(prep: Int?, cook: Int?, servings: Int?): String {
 val unknown = stringResource(R.string.unknown_expiry)
 fun count(value: Int?) = value?.let { Quantities.format(it.toString()) } ?: unknown
 return stringResource(R.string.ai_recipe_facts, count(prep), count(cook), count(servings))
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun RecipeReviewScreen(
 draft: ARecipeDraft, context: AContext?, message: Int?, busy: Boolean, pending: Boolean,
 onBack: () -> Unit, photo: suspend () -> ARecipePhoto?, skipPhoto: () -> Unit,
 edit: ((ARecipe) -> ARecipe) -> Unit, createIngredient: (Int) -> Unit,
 save: () -> Unit, recover: () -> Unit, signIn: () -> Unit
) {
 val recipe = draft.recipe
 val problems = recipe.rowProblems()
 val scroll = rememberScrollState()
 var focus by remember { mutableStateOf<Pair<String, Int>?>(null) }
 var attempt by remember { mutableIntStateOf(0) }
 val scope = rememberCoroutineScope()
 Column(Modifier.fillMaxSize().imePadding()) {
  TextButton(onClick=onBack) { Text(stringResource(R.string.back_to_recipes)) }
  if(busy) LinearProgressIndicator(Modifier.fillMaxWidth())
  Column(Modifier.weight(1f).verticalScroll(scroll).padding(horizontal=16.dp),
   verticalArrangement=Arrangement.spacedBy(8.dp)) {
   AssistantFeedback(message, pending, busy, recover, signIn)
   if (draft.photoKey != null) {
    val preview by produceState<Pair<Boolean, Bitmap?>>(false to null, draft.photoKey) {
     val thumbnail = withContext(Dispatchers.IO) {
      photo()?.thumbnail?.let { encoded ->
       runCatching {
        val bytes = android.util.Base64.decode(encoded, android.util.Base64.DEFAULT)
        BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
       }.getOrNull()
      }
     }
     value = true to thumbnail
    }
    val (loaded, thumbnail) = preview
    if (thumbnail != null) Image(thumbnail.asImageBitmap(), stringResource(R.string.ai_imported_photo),
     Modifier.fillMaxWidth().heightIn(max=180.dp))
    else if (loaded) Text(stringResource(R.string.ai_import_photo_unavailable), color=MaterialTheme.colorScheme.error)
    TextButton(onClick=skipPhoto, enabled=!busy&&!pending) { Text(stringResource(R.string.ai_skip_imported_photo)) }
   }
   if (draft.canEdit) {
    AField(stringResource(R.string.ai_recipe_title),recipe.title,{value->edit{it.copy(title=value.take(150))}})
    AField(stringResource(R.string.ai_description),recipe.description,{value->edit{it.copy(description=value)}},multi=true)
    APicker(stringResource(R.string.ai_slots),context?.categories?:emptyList(),context?.categories?.find{it.categoryId==recipe.categoryId},{it.name},{c->edit{it.copy(categoryId=c.categoryId)}})
    AField(stringResource(R.string.ai_servings),recipe.servings?.toString()?:"",{text->edit{it.copy(servings=text.toIntOrNull())}},numeric=true)
    AField(stringResource(R.string.ai_prep),recipe.prepMinutes?.toString()?:"",{text->edit{it.copy(prepMinutes=text.toIntOrNull())}},numeric=true)
    AField(stringResource(R.string.ai_cook),recipe.cookMinutes?.toString()?:"",{text->edit{it.copy(cookMinutes=text.toIntOrNull())}},numeric=true)
    recipe.ingredients.forEachIndexed { index, line -> key(line.localId) {
     val bring = remember { BringIntoViewRequester() }
     val requester = remember { FocusRequester() }
     val problem = problems[line.localId]
     val invalidQuantity = runCatching { line.reviewQuantity() }.isFailure
     LaunchedEffect(focus) {
      if (focus?.first == line.localId) {
       bring.bringIntoView()
       if (invalidQuantity) requester.requestFocus()
      }
     }
     Card(Modifier.bringIntoViewRequester(bring)) {
      Column(Modifier.padding(12.dp), verticalArrangement=Arrangement.spacedBy(8.dp)) {
       fun change(transform: (ALine)->ALine) = edit { current ->
        current.copy(ingredients=current.ingredients.map { if (it.localId == line.localId) transform(it) else it })
       }
       if (line.original.isNotBlank()) Text(line.original)
       if (problem != null && problem != R.string.invalid_quantity) Text(stringResource(problem), color=MaterialTheme.colorScheme.error)
       AField(stringResource(R.string.ai_ingredient_name),line.name,{value->change{it.copy(name=value)}})
       AIngredientPicker(context,line.name,line.ingredientId,{id,name->change{it.copy(ingredientId=id,name=name)}})
       APicker(stringResource(R.string.purchase_unit),context?.units?:emptyList(),context?.units?.find{it.unitId==line.unitId},{it.abbreviation},{u->change{it.copy(unitId=u.unitId,unit=u.abbreviation)}})
       AField(stringResource(R.string.quantity),line.quantityText ?: Quantities.editor(line.quantity),
        {value->change{it.copy(quantityText=value)}},numeric=true,problem=R.string.ai_quantity_field.takeIf { invalidQuantity },
        modifier=Modifier.focusRequester(requester))
       AField(stringResource(R.string.ai_notes),line.notes?:"",{value->change{it.copy(notes=value)}})
       FlowRow {
        if (line.ingredientId == null) TextButton(onClick={createIngredient(index)},enabled=!pending&&!busy){Text(stringResource(R.string.ai_create_ingredient))}
        TextButton(onClick={edit{it.copy(ingredients=it.ingredients.filterNot { row -> row.localId == line.localId })}}){Text(stringResource(R.string.ai_remove))}
       }
      }
     }
    }}
    TextButton(onClick={edit{it.copy(ingredients=it.ingredients+ALine())}}){Text(stringResource(R.string.ai_add_ingredient))}
    AField(stringResource(R.string.ai_instructions),recipe.instructions,{value->edit{it.copy(instructions=value)}},multi=true)
   } else {
    Text(recipe.title,style=MaterialTheme.typography.headlineSmall)
    Text(recipeFacts(recipe.prepMinutes, recipe.cookMinutes, recipe.servings))
    recipe.ingredients.forEach { line ->
     val quantity = line.quantity?.let { Quantities.format(it) } ?: stringResource(R.string.unknown_expiry)
     Text("${line.name} · $quantity ${line.unit.orEmpty()}")
    }
    Text(recipe.instructions)
   }
   recipe.source?.let { Text(it) }
  }
  if (draft.canEdit) Surface(tonalElevation=4.dp) {
   Column(Modifier.fillMaxWidth().padding(horizontal=16.dp, vertical=6.dp)) {
    if (problems.isNotEmpty()) Text(stringResource(R.string.ai_unresolved_rows,problems.size),color=MaterialTheme.colorScheme.error)
    Button(onClick={
     problems.keys.firstOrNull()?.let { focus=it to ++attempt }
     if (problems.isEmpty() && recipe.reviewProblem() != null) scope.launch { scroll.scrollTo(0) }
     save()
    }, enabled=!busy&&!pending, modifier=Modifier.fillMaxWidth()) { Text(stringResource(R.string.ai_save_recipe)) }
   }
  }
 }
}
@Composable private fun taskLabel(kind:String)=stringResource(when(kind){"suggest"->R.string.ai_task_suggest;"import"->R.string.ai_task_import;"generate"->R.string.ai_task_generate;"plan"->R.string.ai_task_plan;"receipt"->R.string.ai_task_receipt;else->R.string.ai_task_recipe_photo})
@Composable private fun jobStateLabel(state:String)=stringResource(when(state){"Queued","Running"->R.string.ai_waiting;"Completed"->R.string.ai_review;"Cancelled"->R.string.ai_cancelled;else->R.string.ai_unavailable})
@OptIn(ExperimentalMaterial3Api::class)
@Composable private fun ADate(label:String,value:String?,onChange:(String?)->Unit){var open by remember{mutableStateOf(false)};Row{OutlinedButton(onClick={open=true},modifier=Modifier.weight(1f)){Text(label+": "+(Quantities.date(value)?:stringResource(R.string.unknown_date)))};TextButton(onClick={onChange(null)}){Text(stringResource(R.string.clear))}};if(open){val state=rememberDatePickerState(initialSelectedDateMillis=value?.let{runCatching{LocalDate.parse(it).atStartOfDay(java.time.ZoneOffset.UTC).toInstant().toEpochMilli()}.getOrNull()});DatePickerDialog(onDismissRequest={open=false},confirmButton={TextButton(onClick={state.selectedDateMillis?.let{onChange(java.time.Instant.ofEpochMilli(it).atZone(java.time.ZoneOffset.UTC).toLocalDate().toString())};open=false}){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={open=false}){Text(stringResource(R.string.cancel))}}){DatePicker(state)}}}

@OptIn(ExperimentalLayoutApi::class)
@Composable private fun PhotoInput(onUpload:(ByteArray)->Unit){
 val context=LocalContext.current;var image by remember{mutableStateOf<Bitmap?>(null)};var error by remember{mutableStateOf(false)};val cameraFile=remember{java.io.File(java.io.File(context.cacheDir,"ai-camera").apply{mkdirs()},UUID.randomUUID().toString()+".jpg")}
 fun decode(uri:Uri){error=false;try{val info=BitmapFactory.Options().apply{inJustDecodeBounds=true};context.contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,info)};require(info.outWidth>0&&info.outHeight>0);val options=BitmapFactory.Options().apply{inSampleSize=max(1,max(info.outWidth,info.outHeight)/2400)};image=context.contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,options)};require(image!=null)}catch(e:Exception){error=true}}
 val select=rememberLauncherForActivityResult(ActivityResultContracts.GetContent()){uri->uri?.let{decode(it)}}
 val camera=rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()){ok->if(ok)decode(FileProvider.getUriForFile(context,context.packageName+".photos",cameraFile));cameraFile.delete()}
 FlowRow{TextButton(onClick={select.launch("image/*")}){Text(stringResource(R.string.ai_photo))};TextButton(onClick={camera.launch(FileProvider.getUriForFile(context,context.packageName+".photos",cameraFile))}){Text(stringResource(R.string.ai_camera))}}
 if(error)Text(stringResource(R.string.ai_invalid_photo));image?.let{bitmap->PhotoReview(bitmap,onDismiss={image=null},onAccept={bytes->image=null;onUpload(bytes)})}
 DisposableEffect(Unit){onDispose{cameraFile.delete()}}
}
@OptIn(ExperimentalLayoutApi::class)
@Composable private fun PhotoReview(original:Bitmap,onDismiss:()->Unit,onAccept:(ByteArray)->Unit){
 var bitmap by remember{mutableStateOf(original.copy(Bitmap.Config.ARGB_8888,true))};var selection by remember{mutableStateOf<Rect?>(null)};var origin by remember{mutableStateOf(Offset.Zero)};var canvasSize by remember{mutableStateOf(IntSize(1,1))}
 Dialog(onDismissRequest=onDismiss,properties=DialogProperties(usePlatformDefaultWidth=false)){Surface(Modifier.fillMaxWidth().padding(12.dp)){Column(Modifier.verticalScroll(rememberScrollState()).padding(16.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
  Text(stringResource(R.string.ai_image_hint));Canvas(Modifier.fillMaxWidth().height(320.dp).pointerInput(bitmap){detectDragGestures(onDragStart={origin=it;selection=Rect(it,it)},onDrag={change,_->selection=Rect(min(origin.x,change.position.x),min(origin.y,change.position.y),max(origin.x,change.position.x),max(origin.y,change.position.y));change.consume()})}){canvasSize=IntSize(size.width.toInt(),size.height.toInt());drawImage(bitmap.asImageBitmap(),dstSize=canvasSize);selection?.let{drawRect(androidx.compose.ui.graphics.Color.Black.copy(alpha=.5f),it.topLeft,it.size)}}
  fun selected():android.graphics.Rect?{val rect=selection?:return null;val sx=bitmap.width.toFloat()/canvasSize.width;val sy=bitmap.height.toFloat()/canvasSize.height;return android.graphics.Rect((rect.left*sx).toInt().coerceIn(0,bitmap.width),(rect.top*sy).toInt().coerceIn(0,bitmap.height),(rect.right*sx).toInt().coerceIn(0,bitmap.width),(rect.bottom*sy).toInt().coerceIn(0,bitmap.height)).takeIf{it.width()>1&&it.height()>1}}
  FlowRow{TextButton(onClick={selected()?.let{r->bitmap=Bitmap.createBitmap(bitmap,r.left,r.top,r.width(),r.height());selection=null}}){Text(stringResource(R.string.ai_crop))};TextButton(onClick={selected()?.let{r->val next=bitmap.copy(Bitmap.Config.ARGB_8888,true);android.graphics.Canvas(next).drawRect(r,android.graphics.Paint().apply{color=android.graphics.Color.BLACK;style=android.graphics.Paint.Style.FILL});bitmap=next;selection=null}}){Text(stringResource(R.string.ai_redact))};TextButton(onClick={bitmap=Bitmap.createBitmap(bitmap,0,0,bitmap.width,bitmap.height,Matrix().apply{postRotate(90f)},true);selection=null}){Text(stringResource(R.string.ai_rotate))}}
  Button(onClick={val output=java.io.ByteArrayOutputStream();bitmap.compress(Bitmap.CompressFormat.JPEG,88,output);onAccept(output.toByteArray())}){Text(stringResource(R.string.ai_upload_reviewed))};TextButton(onClick=onDismiss){Text(stringResource(R.string.cancel))}
 }}}
}
