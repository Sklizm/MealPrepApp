package app.mealprep.companion

import android.Manifest
import android.os.Bundle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.core.view.WindowCompat
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.ShoppingCart
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import kotlinx.serialization.decodeFromString
import java.time.*
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

class MainActivity:AppCompatActivity(){
 override fun onCreate(savedInstanceState:Bundle?){super.onCreate(savedInstanceState);enableEdgeToEdge();val graph=(application as MealPrepApplication).graph
  setContent{val settings by graph.settings.collectAsState();val preferences=settings
   if(preferences==null){Box(Modifier.fillMaxSize(),contentAlignment=Alignment.Center){CircularProgressIndicator()}}
   else{
    LaunchedEffect(preferences.language){val locales=LocaleListCompat.forLanguageTags(preferences.language);if(AppCompatDelegate.getApplicationLocales()!=locales)AppCompatDelegate.setApplicationLocales(locales)}
    val dark=when(preferences.theme){"dark"->true;"light"->false;else->androidx.compose.foundation.isSystemInDarkTheme()}
    SideEffect{WindowCompat.getInsetsController(window,window.decorView).apply{isAppearanceLightStatusBars=!dark;isAppearanceLightNavigationBars=!dark}}
    MaterialTheme(colorScheme=palette(dark)){Surface(Modifier.fillMaxSize()){CompanionApp(graph,preferences,intent.getBooleanExtra("pantry",false))}}
   }
  }
 }
 override fun onStart(){super.onStart();(application as MealPrepApplication).graph.requestSync()}
}
private fun palette(dark:Boolean)=if(dark)darkColorScheme(primary=Color(0xFFACD2B2),onPrimary=Color(0xFF243B2C),primaryContainer=Color(0xFF50654F),onPrimaryContainer=Color(0xFFF0EBDD),secondary=Color(0xFFD4C2AA),onSecondary=Color(0xFF3F3026),secondaryContainer=Color(0xFF52634C),onSecondaryContainer=Color(0xFFF0EBDD),background=Color(0xFF34483E),onBackground=Color(0xFFF0EBDD),surface=Color(0xFF40574B),onSurface=Color(0xFFF0EBDD),surfaceVariant=Color(0xFF4C6256),onSurfaceVariant=Color(0xFFE6DECE),outline=Color(0xFFAFC0AD),surfaceContainer=Color(0xFF40574B),surfaceContainerLow=Color(0xFF3B5145),surfaceContainerLowest=Color(0xFF34483E),surfaceContainerHigh=Color(0xFF465D50),surfaceContainerHighest=Color(0xFF4C6256),surfaceDim=Color(0xFF34483E),surfaceBright=Color(0xFF4C6256),error=Color(0xFFFFB4A9))else lightColorScheme(primary=Color(0xFF78513B),onPrimary=Color(0xFFFFF8ED),primaryContainer=Color(0xFFD1C1AD),onPrimaryContainer=Color(0xFF3F3026),secondary=Color(0xFF47654F),onSecondary=Color.White,secondaryContainer=Color(0xFFC8D3BE),onSecondaryContainer=Color(0xFF263D2B),background=Color(0xFFE6D8C5),onBackground=Color(0xFF3F3026),surface=Color(0xFFF0E5D5),onSurface=Color(0xFF3F3026),surfaceVariant=Color(0xFFE4D3BE),onSurfaceVariant=Color(0xFF4A3A2E),outline=Color(0xFF8C7763),surfaceContainer=Color(0xFFF0E5D5),surfaceContainerLow=Color(0xFFF5ECDD),surfaceContainerLowest=Color(0xFFFFF8ED),surfaceContainerHigh=Color(0xFFEADCC9),surfaceContainerHighest=Color(0xFFE4D3BE),surfaceDim=Color(0xFFE6D8C5),surfaceBright=Color(0xFFFFF8ED),error=Color(0xFF9E312C))
private inline fun<reified T> List<CacheEntity>.records(kind:String)=filter{it.kind==kind}.map{WireJson.decodeFromString<T>(it.payload)}
private fun date(text:String?)=text?.let{LocalDate.parse(it).format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM))}
private fun errorText(context:android.content.Context,error:Throwable)=context.getString(when{error is PendingReviewRequired->R.string.pending_review_hint;error is LocalVersionConflict->R.string.local_conflict;error is ApiFailure&&error.status==409->R.string.version_conflict;error is ApiFailure&&error.status==401->R.string.session_expired;error is ApiFailure&&error.status==423->R.string.account_locked;error is java.io.IOException->R.string.connection_required;error is IllegalArgumentException->R.string.invalid_quantity;else->R.string.operation_failed})

@Composable private fun CompanionApp(graph:AppGraph,settings:PreferencesState,startPantry:Boolean){
 val session by graph.session.collectAsState();val context=LocalContext.current;val scope=rememberCoroutineScope();var error by remember{mutableStateOf<String?>(null)}
 if(session==null){LoginScreen(settings.lastServer,onLogin={server,user,password->graph.login(server,user,password)});return}
 val active=session!!;val repository=remember(active.accountKey){graph.repository()!!}
 key(active.accountKey){
  val items by repository.items.collectAsState(initial=emptyList());val cache by repository.cache.collectAsState(initial=emptyList());val pending by repository.outbox.collectAsState(initial=emptyList());val sync by repository.state.collectAsState()
  var page by rememberSaveable{mutableIntStateOf(if(startPantry)1 else 0)};var showLogin by remember{mutableStateOf(false)};var signOut by remember{mutableStateOf(false)}
  val reviews=remember(cache){cache.records<OnlineReview>("review")};var recovery by remember{mutableStateOf<OnlineReview?>(null)};val pendingCount=pending.size+reviews.size
  var busy by remember{mutableStateOf(false)}
  fun action(block:suspend()->Unit){scope.launch{busy=true;try{block()}catch(e:Exception){if(e is kotlinx.coroutines.CancellationException)throw e;error=errorText(context,e)}finally{busy=false}}}
  Scaffold(containerColor=MaterialTheme.colorScheme.background,contentWindowInsets=WindowInsets.safeDrawing,
   bottomBar={NavigationBar{listOf(R.string.shopping,R.string.pantry,R.string.settings).forEachIndexed{index,title->NavigationBarItem(selected=page==index,onClick={page=index},icon={Icon(when(index){0->Icons.Default.ShoppingCart;1->Icons.Default.Home;else->Icons.Default.Settings},null)},label={Text(stringResource(title))})}}}){padding->
   Column(Modifier.padding(padding).fillMaxSize()){
    Row(Modifier.fillMaxWidth().padding(horizontal=16.dp,vertical=8.dp),verticalAlignment=Alignment.CenterVertically){Image(painterResource(R.drawable.mealprep_icon),null,Modifier.size(40.dp));Column(Modifier.weight(1f).padding(start=10.dp)){Text(stringResource(R.string.app_name),style=MaterialTheme.typography.titleLarge);Text(active.username,style=MaterialTheme.typography.bodySmall)};TextButton(onClick={graph.requestSync()},enabled=!busy){Text(stringResource(R.string.refresh))}}
    Row(Modifier.fillMaxWidth().padding(horizontal=16.dp),verticalAlignment=Alignment.CenterVertically){Text(stringResource(when(sync){SyncState.Syncing->R.string.syncing;SyncState.Current->R.string.synced;SyncState.AuthenticationRequired->R.string.session_expired;SyncState.Conflict->R.string.conflict_pending;else->R.string.cached_offline}),Modifier.weight(1f),style=MaterialTheme.typography.bodySmall);if(sync==SyncState.AuthenticationRequired)TextButton(onClick={showLogin=true}){Text(stringResource(R.string.sign_in))}}
    if(sync==SyncState.Syncing||busy)LinearProgressIndicator(Modifier.fillMaxWidth())
    if(reviews.isNotEmpty())TextButton(onClick={recovery=reviews.first()},enabled=!busy){Text(stringResource(R.string.pending_review))}
    when(page){0->ShoppingScreen(graph,repository,items,cache,pending,busy,{action(it)},{error=it});1->PantryScreen(repository,cache,busy,{action(it)});else->SettingsScreen(graph,settings,pendingCount,{signOut=true})}
   }
  }
  recovery?.let{review->AlertDialog(onDismissRequest={recovery=null},title={Text(stringResource(R.string.pending_review))},text={Column{Text(stringResource(R.string.pending_review_hint));Text(review.summary)}},confirmButton={TextButton(onClick={action{repository.resumeReview(review);recovery=null}},enabled=!busy){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={recovery=null}){Text(stringResource(R.string.cancel))}})}
  if(showLogin)AlertDialog(onDismissRequest={showLogin=false},title={Text(stringResource(R.string.sign_in))},text={LoginScreen(active.baseUrl,onLogin={server,user,password->graph.login(server,user,password);showLogin=false})},confirmButton={},dismissButton={TextButton(onClick={showLogin=false}){Text(stringResource(R.string.cancel))}})
  if(signOut)AlertDialog(onDismissRequest={signOut=false},title={Text(stringResource(R.string.sign_out))},text={Text(stringResource(if(pendingCount==0)R.string.sign_out_clear else R.string.sign_out_pending,pendingCount))},confirmButton={TextButton(onClick={action{graph.signOut()};signOut=false}){Text(stringResource(R.string.sign_out_and_clear))}},dismissButton={TextButton(onClick={signOut=false}){Text(stringResource(R.string.cancel))}})
  if(error!=null)AlertDialog(onDismissRequest={error=null},text={Text(error!!)},confirmButton={TextButton(onClick={error=null}){Text(stringResource(R.string.close))}})
 }
}
@Composable private fun LoginScreen(initialServer:String,onLogin:suspend(String,String,String)->Unit){
 var server by rememberSaveable{mutableStateOf(initialServer)};var user by rememberSaveable{mutableStateOf("")};var password by remember{mutableStateOf("")};var busy by remember{mutableStateOf(false)};var error by remember{mutableStateOf<String?>(null)};val scope=rememberCoroutineScope();val context=LocalContext.current
 Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(24.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
  Image(painterResource(R.drawable.mealprep_icon),null,Modifier.size(72.dp));Text(stringResource(R.string.app_name),style=MaterialTheme.typography.headlineMedium);Text(stringResource(R.string.private_connection_hint))
  OutlinedTextField(server,{server=it},label={Text(stringResource(R.string.server_url))},modifier=Modifier.fillMaxWidth(),singleLine=true,keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Uri))
  OutlinedTextField(user,{user=it},label={Text(stringResource(R.string.username))},modifier=Modifier.fillMaxWidth(),singleLine=true)
  OutlinedTextField(password,{password=it},label={Text(stringResource(R.string.password))},modifier=Modifier.fillMaxWidth(),singleLine=true,visualTransformation=PasswordVisualTransformation(),keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Password))
  error?.let{Text(it,color=MaterialTheme.colorScheme.error)}
  Button(onClick={scope.launch{busy=true;try{onLogin(server,user,password);password=""}catch(e:Exception){error=context.getString(if(e is ApiFailure&&e.status==401)R.string.invalid_credentials else if(e is ApiFailure&&e.status==423)R.string.account_locked else R.string.connection_required)}finally{busy=false}}},enabled=!busy&&server.isNotBlank()&&user.isNotBlank()&&password.isNotBlank()){Text(stringResource(R.string.sign_in))}
  if(busy)CircularProgressIndicator()
 }
}
@Composable private fun <T> Picker(label:String,values:List<T>,selected:T?,display:(T)->String,onChoose:(T)->Unit,enabled:Boolean=true){
 var expanded by remember{mutableStateOf(false)}
 Box{OutlinedButton(onClick={expanded=true},enabled=enabled,modifier=Modifier.fillMaxWidth()){Text(selected?.let(display)?:label)};DropdownMenu(expanded=expanded,onDismissRequest={expanded=false},modifier=Modifier.heightIn(max=320.dp)){values.forEach{value->DropdownMenuItem(text={Text(display(value))},onClick={onChoose(value);expanded=false})}}}
}
@Composable private fun ShoppingScreen(graph:AppGraph,repository:CompanionRepository,entities:List<ItemEntity>,cache:List<CacheEntity>,pending:List<OutboxEntity>,busy:Boolean,action:(suspend()->Unit)->Unit,onError:(String)->Unit){
 val lists=remember(cache){cache.records<ShoppingList>("list").sortedByDescending{it.snapshotId}};val ingredients=remember(cache){cache.records<CatalogIngredient>("ingredient").sortedBy{it.name}};val units=remember(cache){cache.records<CatalogUnit>("unit")};var selected by rememberSaveable{mutableStateOf<Int?>(null)}
 LaunchedEffect(repository.session.accountKey){selected=graph.selectedList()};val list=lists.find{it.snapshotId==selected}?:lists.firstOrNull();val scope=rememberCoroutineScope();val context=LocalContext.current
 var showPurchased by rememberSaveable{mutableStateOf(false)};var add by remember{mutableStateOf(false)};var manualName by rememberSaveable{mutableStateOf("")};var edit by remember{mutableStateOf<ItemEntity?>(null)};var conflict by remember{mutableStateOf<ItemEntity?>(null)};var review by remember{mutableStateOf<Grocery?>(null)}
 val rows=entities.filter{it.snapshotId==list?.snapshotId}.map{it to WireJson.decodeFromString<Grocery>(it.payload)};val visible=rows.filter{!it.second.isDeleted&&(showPurchased||!it.second.isPurchased||it.first.hasConflict)}
 Column(Modifier.fillMaxSize().padding(horizontal=16.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
  Text(stringResource(R.string.shopping),style=MaterialTheme.typography.headlineSmall)
  Picker(stringResource(R.string.saved_lists),lists,list,{"#${it.snapshotId} · ${date(it.startDate)} – ${date(it.endDate)}"},{selected=it.snapshotId;scope.launch{graph.selectList(it.snapshotId)}})
  Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){Button(onClick={add=true},enabled=list!=null){Text(stringResource(R.string.add_grocery))};TextButton(onClick={action{selected=repository.createList()}},enabled=!busy){Text(stringResource(R.string.new_list))}}
  Text(stringResource(R.string.shopping_progress,rows.count{it.second.isPurchased&&!it.second.isDeleted},rows.count{!it.second.isDeleted},pending.size),style=MaterialTheme.typography.bodySmall)
  Row(verticalAlignment=Alignment.CenterVertically){Checkbox(showPurchased,{showPurchased=it});Text(stringResource(R.string.show_purchased))}
  if(visible.isEmpty())Text(stringResource(if(list==null)R.string.shopping_no_list else if(rows.any{!it.second.isDeleted})R.string.shopping_complete else R.string.shopping_empty),Modifier.padding(vertical=16.dp))
  LazyColumn(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(8.dp),contentPadding=PaddingValues(bottom=16.dp)){
   items(visible,key={it.first.localId}){(entity,item)->Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(6.dp)){
    Row(verticalAlignment=Alignment.CenterVertically){Checkbox(item.isPurchased,{value->scope.launch{try{repository.change(entity.localId,entity.version,item.copy(isPurchased=value))}catch(e:Exception){onError(errorText(context,e))}}},enabled=!entity.hasConflict&&!busy);Text(item.name,Modifier.weight(1f),style=MaterialTheme.typography.titleMedium)}
    Text(stringResource(R.string.required_quantity,Quantities.format(item.requiredQuantity),item.unitAbbreviation),style=MaterialTheme.typography.bodySmall)
    if(item.isPurchased)Text(stringResource(R.string.purchased_quantity,Quantities.format(item.actualQuantity),item.actualUnit),style=MaterialTheme.typography.bodySmall)
    if(pending.any{it.localId==entity.localId})Text(stringResource(if(entity.hasConflict)R.string.conflict_pending else R.string.queued_change),color=if(entity.hasConflict)MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant)
    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)){
     TextButton(onClick={edit=entity},enabled=!entity.hasConflict){Text(stringResource(R.string.edit))}
     if(entity.hasConflict)TextButton(onClick={conflict=entity}){Text(stringResource(R.string.resolve_conflict))}
     if(item.isImported)Text(stringResource(R.string.imported),Modifier.align(Alignment.CenterVertically),style=MaterialTheme.typography.bodySmall)
     else if(item.isPurchased&&item.ingredientId!=null&&(item.purchasedUnitId?:item.unitId)!=null)TextButton(onClick={action{review=repository.reviewPurchase(entity.localId)}},enabled=!busy&&pending.none{it.localId==entity.localId}){Text(stringResource(R.string.import_pantry))}
    }
    if(item.isPurchased&&!item.isImported&&(item.ingredientId==null||(item.purchasedUnitId?:item.unitId)==null))Text(stringResource(R.string.mapping_needed),style=MaterialTheme.typography.bodySmall)
   }}}
  }
 }
 if(add)AlertDialog(onDismissRequest={add=false},title={Text(stringResource(R.string.add_grocery))},text={OutlinedTextField(manualName,{manualName=it.take(150)},label={Text(stringResource(R.string.grocery_name))},singleLine=true)},confirmButton={TextButton(onClick={val id=list?.snapshotId?:return@TextButton;scope.launch{try{repository.addManual(id,manualName);manualName="";add=false}catch(e:Exception){onError(errorText(context,e))}}},enabled=manualName.isNotBlank()){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={add=false}){Text(stringResource(R.string.cancel))}})
 edit?.let{row->GroceryEditor(WireJson.decodeFromString(row.payload),ingredients,units,onDismiss={edit=null},onSave={desired->scope.launch{try{repository.change(row.localId,row.version,desired);edit=null}catch(e:Exception){onError(errorText(context,e))}}})}
 conflict?.let{row->val current=entities.find{it.localId==row.localId}?:row;val remote=current.conflictPayload?.let{WireJson.decodeFromString<Grocery>(it)};val wanted=WireJson.decodeFromString<Grocery>(current.payload)
  AlertDialog(onDismissRequest={conflict=null},title={Text(stringResource(R.string.resolve_conflict))},text={Column(verticalArrangement=Arrangement.spacedBy(8.dp)){Text(stringResource(R.string.conflict_hint));Text(stringResource(R.string.your_value,wanted.name,Quantities.format(wanted.actualQuantity),wanted.actualUnit));ConflictDetails(wanted);Text(if(remote==null)stringResource(R.string.refresh_conflict)else if(remote.isDeleted)stringResource(R.string.server_deleted)else stringResource(R.string.saved_value,remote.name,Quantities.format(remote.actualQuantity),remote.actualUnit));if(remote!=null)ConflictDetails(remote)}},confirmButton={TextButton(onClick={if(remote!=null)action{repository.resolve(row.localId,remote.version,true);conflict=null}},enabled=remote!=null){Text(stringResource(if(remote?.isDeleted==true)R.string.save_as_new else R.string.keep_mine))}},dismissButton={Row{TextButton(onClick={graph.requestSync()}){Text(stringResource(R.string.refresh))};TextButton(onClick={if(remote!=null)action{repository.resolve(row.localId,remote.version,false);conflict=null}},enabled=remote!=null){Text(stringResource(R.string.use_saved))}}})
 }
 review?.let{item->AlertDialog(onDismissRequest={review=null},title={Text(stringResource(R.string.review_import))},text={Column(verticalArrangement=Arrangement.spacedBy(8.dp)){Text(item.name);Text(stringResource(R.string.required_quantity,Quantities.format(item.requiredQuantity),item.unitAbbreviation));Text(stringResource(R.string.purchased_quantity,Quantities.format(item.actualQuantity),item.actualUnit));Text(stringResource(R.string.expiry_value,date(item.expiresOn)?:stringResource(R.string.unknown_expiry)));Text(stringResource(R.string.import_hint))}},confirmButton={TextButton(onClick={action{repository.importReviewed(item);review=null}},enabled=!busy){Text(stringResource(R.string.import_pantry))}},dismissButton={TextButton(onClick={review=null}){Text(stringResource(R.string.cancel))}})}
}
@OptIn(ExperimentalMaterial3Api::class)
@Composable private fun DateField(label:String,value:String?,enabled:Boolean=true,onChange:(String?)->Unit){
 var open by remember{mutableStateOf(false)}
 Row(verticalAlignment=Alignment.CenterVertically){OutlinedButton(onClick={open=true},enabled=enabled,modifier=Modifier.weight(1f)){Text("$label: ${date(value)?:stringResource(R.string.unknown_date)}")};TextButton(onClick={onChange(null)},enabled=enabled&&value!=null){Text(stringResource(R.string.clear))}}
 if(open){val state=rememberDatePickerState(initialSelectedDateMillis=value?.let{LocalDate.parse(it).atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli()});DatePickerDialog(onDismissRequest={open=false},confirmButton={TextButton(onClick={state.selectedDateMillis?.let{onChange(Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString())};open=false}){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={open=false}){Text(stringResource(R.string.cancel))}}){DatePicker(state)}}
}
@Composable private fun GroceryEditor(item:Grocery,ingredients:List<CatalogIngredient>,units:List<CatalogUnit>,onDismiss:()->Unit,onSave:(Grocery)->Unit){
 var name by rememberSaveable{mutableStateOf(item.name)};var quantity by rememberSaveable{mutableStateOf(item.purchasedQuantity?:item.requiredQuantity)};var ingredient by rememberSaveable{mutableStateOf(item.ingredientId)};var unit by rememberSaveable{mutableStateOf(item.purchasedUnitId?:item.unitId)};var purchased by rememberSaveable{mutableStateOf(item.isPurchased)};var expiry by rememberSaveable{mutableStateOf(item.expiresOn)};var purchasedOn by rememberSaveable{mutableStateOf(item.purchasedOn)};var search by rememberSaveable{mutableStateOf("")};var invalid by remember{mutableStateOf(false)}
 AlertDialog(onDismissRequest=onDismiss,title={Text(stringResource(R.string.edit_grocery))},text={Column(Modifier.verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(8.dp)){
  OutlinedTextField(name,{name=it.take(150)},label={Text(stringResource(R.string.grocery_name))});Row(verticalAlignment=Alignment.CenterVertically){Checkbox(purchased,{purchased=it});Text(stringResource(R.string.purchased))}
  Text(stringResource(R.string.required_quantity,Quantities.format(item.requiredQuantity),item.unitAbbreviation))
  OutlinedTextField(quantity,{quantity=it},label={Text(stringResource(R.string.actual_purchase))},enabled=!item.isImported,keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Decimal))
  OutlinedTextField(search,{search=it},label={Text(stringResource(R.string.search_ingredient))},enabled=!item.isImported)
  Picker(stringResource(R.string.optional_ingredient),ingredients.filter{it.name.contains(search,true)}.take(80),ingredients.find{it.ingredientId==ingredient},{it.name},{ingredient=it.ingredientId;if(unit==null)unit=it.defaultUnitId},enabled=!item.isImported)
  Picker(stringResource(R.string.purchase_unit),units,units.find{it.unitId==unit},{it.abbreviation},{unit=it.unitId},enabled=!item.isImported)
  DateField(stringResource(R.string.purchase_date),purchasedOn,!item.isImported){purchasedOn=it};DateField(stringResource(R.string.expiry_date),expiry,!item.isImported){expiry=it}
  if(item.isImported)Text(stringResource(R.string.imported_correction_hint))else Text(stringResource(R.string.mapping_optional))
  if(invalid)Text(stringResource(R.string.invalid_quantity),color=MaterialTheme.colorScheme.error)
 }},confirmButton={TextButton(onClick={try{require(name.isNotBlank());val q=if(item.isImported)item.purchasedQuantity else Quantities.input(quantity);require(expiry==null||purchasedOn==null||!LocalDate.parse(expiry).isBefore(LocalDate.parse(purchasedOn)));onSave(item.copy(name=name.trim(),ingredientId=if(item.isImported)item.ingredientId else ingredient,unitId=item.unitId?:unit,isPurchased=purchased,purchasedQuantity=q,purchasedUnitId=if(item.isImported)item.purchasedUnitId else unit,purchasedUnitAbbreviation=units.find{it.unitId==unit}?.abbreviation?:item.purchasedUnitAbbreviation,purchasedOn=purchasedOn,expiresOn=expiry))}catch(e:IllegalArgumentException){invalid=true}}){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick=onDismiss){Text(stringResource(R.string.cancel))}})
}
@Composable private fun PantryScreen(repository:CompanionRepository,cache:List<CacheEntity>,busy:Boolean,action:(suspend()->Unit)->Unit){
 val batches=remember(cache){cache.records<StockBatch>("batch")};val thresholds=remember(cache){cache.records<Threshold>("threshold")};val units=remember(cache){cache.records<CatalogUnit>("unit")};val maps=remember(cache){cache.records<UnitMapping>("map")};val groups=remember(batches,thresholds,units,maps){pantryGroups(batches,thresholds,units,maps)}
 var search by rememberSaveable{mutableStateOf("")};var expanded by rememberSaveable{mutableStateOf(listOf<String>())};var review by remember{mutableStateOf<StockBatch?>(null)};var threshold by remember{mutableStateOf<Threshold?>(null)}
 Column(Modifier.fillMaxSize().padding(horizontal=16.dp),verticalArrangement=Arrangement.spacedBy(8.dp)){
  Text(stringResource(R.string.pantry),style=MaterialTheme.typography.headlineSmall);Text(stringResource(R.string.cached_pantry_hint),style=MaterialTheme.typography.bodySmall)
  OutlinedTextField(search,{search=it},label={Text(stringResource(R.string.search_pantry))},modifier=Modifier.fillMaxWidth(),singleLine=true)
  if(groups.isEmpty())Text(stringResource(R.string.pantry_empty),Modifier.padding(vertical=16.dp))
  LazyColumn(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(8.dp),contentPadding=PaddingValues(bottom=16.dp)){
   items(groups.filter{it.name.contains(search,true)},key={"${it.ingredientId}:${it.unitId}"}){group->val id="${group.ingredientId}:${group.unitId}";Card{Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(6.dp)){
    Text(group.name,style=MaterialTheme.typography.titleMedium);Text("${Quantities.format(group.total.toPlainString())} ${group.unit}");Text(stringResource(R.string.available_stock,Quantities.format(group.available.toPlainString()),group.unit),style=MaterialTheme.typography.bodySmall)
    if(group.low)Text(stringResource(R.string.low_stock),color=MaterialTheme.colorScheme.error)
    TextButton(onClick={expanded=if(id in expanded)expanded-id else expanded+id}){Text(stringResource(R.string.purchase_batches,group.batches.size))}
    if(id in expanded){for(batch in group.batches){HorizontalDivider();Text("${Quantities.format(batch.remainingQuantity)} ${batch.unitAbbreviation}");Text(stringResource(R.string.expiry_value,date(batch.expiresOn)?:stringResource(R.string.unknown_expiry)),color=if(batch.expiresOn!=null&&LocalDate.parse(batch.expiresOn).isBefore(LocalDate.now()))MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurfaceVariant);batch.purchasedOn?.let{Text(stringResource(R.string.purchase_date_value,date(it)!!),style=MaterialTheme.typography.bodySmall)};TextButton(onClick={action{review=repository.reviewBatch(batch.batchId)}},enabled=!busy){Text(stringResource(R.string.adjust_batch))}}
     val related=thresholds.filter{it.ingredientId==group.ingredientId&&(maps.find{m->m.unitId==it.unitId}?.canonicalUnitId?:it.unitId)==group.unitId}
     if(related.isEmpty())TextButton(onClick={action{threshold=repository.reviewThreshold(group.ingredientId,group.unitId)}},enabled=!busy){Text(stringResource(R.string.set_threshold))}
     else related.forEach{t->TextButton(onClick={action{threshold=repository.reviewThreshold(t.ingredientId,t.unitId)}},enabled=!busy){Text(stringResource(R.string.threshold_value,Quantities.format(t.quantity),units.find{it.unitId==t.unitId}?.abbreviation?:group.unit))}}
    }
   }}}
  }
 }
 review?.let{batch->var delta by rememberSaveable(batch.batchId,batch.version){mutableStateOf("")};var discard by remember{mutableStateOf(false)};var remove by remember{mutableStateOf(true)};var invalid by remember{mutableStateOf(false)}
  AlertDialog(onDismissRequest={review=null},title={Text(stringResource(R.string.adjust_batch))},text={Column(verticalArrangement=Arrangement.spacedBy(8.dp)){Text(batch.ingredientName);Text("${Quantities.format(batch.remainingQuantity)} ${batch.unitAbbreviation}");Text(stringResource(R.string.expiry_value,date(batch.expiresOn)?:stringResource(R.string.unknown_expiry)));Text(stringResource(R.string.adjust_hint));Row(verticalAlignment=Alignment.CenterVertically){Checkbox(remove,{remove=it});Text(stringResource(R.string.remove_stock))};OutlinedTextField(delta,{delta=it},label={Text(stringResource(R.string.quantity_delta))},keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Decimal));Row(verticalAlignment=Alignment.CenterVertically){Checkbox(discard,{discard=it});Text(stringResource(R.string.record_waste))};if(invalid)Text(stringResource(R.string.invalid_quantity),color=MaterialTheme.colorScheme.error)}},confirmButton={TextButton(onClick={try{val amount=Quantities.input(delta);val normalized=if(remove)"-$amount" else amount;require(!discard||remove);require(batch.remainingQuantity.toBigDecimal()+normalized.toBigDecimal()>=java.math.BigDecimal.ZERO);action{repository.adjustReviewed(batch,normalized,discard);review=null}}catch(e:IllegalArgumentException){invalid=true}},enabled=!busy){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={review=null}){Text(stringResource(R.string.cancel))}})
 }
 threshold?.let{t->var value by rememberSaveable(t.ingredientId,t.unitId,t.version){mutableStateOf(t.quantity)};var invalid by remember{mutableStateOf(false)}
  AlertDialog(onDismissRequest={threshold=null},title={Text(stringResource(R.string.set_threshold))},text={Column(verticalArrangement=Arrangement.spacedBy(8.dp)){Text(stringResource(R.string.threshold_hint));Text(units.find{it.unitId==t.unitId}?.abbreviation?:"");OutlinedTextField(value,{value=it},keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Decimal),label={Text(stringResource(R.string.quantity))});if(invalid)Text(stringResource(R.string.invalid_quantity),color=MaterialTheme.colorScheme.error)}},confirmButton={TextButton(onClick={try{val normalized=Quantities.input(value,allowZero=true);action{repository.setThreshold(t,normalized);threshold=null}}catch(e:IllegalArgumentException){invalid=true}},enabled=!busy){Text(stringResource(R.string.save))}},dismissButton={TextButton(onClick={threshold=null}){Text(stringResource(R.string.cancel))}})
 }
}
@Composable private fun SettingsScreen(graph:AppGraph,settings:PreferencesState,pending:Int,onSignOut:()->Unit){
 val scope=rememberCoroutineScope();val notifications=rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()){};var days by rememberSaveable(settings.expiryDays){mutableStateOf(settings.expiryDays.toString())}
 Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),verticalArrangement=Arrangement.spacedBy(12.dp)){
  Text(stringResource(R.string.settings),style=MaterialTheme.typography.headlineSmall)
  Picker(stringResource(R.string.language),listOf("en","ro","ru"),settings.language,{when(it){"ro"->"Română";"ru"->"Русский";else->"English"}},{scope.launch{graph.update(language=it)}})
  val labels=listOf(stringResource(R.string.theme_system),stringResource(R.string.theme_light),stringResource(R.string.theme_dark));Picker(stringResource(R.string.theme),listOf("system","light","dark"),settings.theme,{labels[listOf("system","light","dark").indexOf(it)]},{scope.launch{graph.update(theme=it)}})
  Row(verticalAlignment=Alignment.CenterVertically){Checkbox(settings.reminders,{scope.launch{graph.update(reminders=it)}});Text(stringResource(R.string.expiry_reminders))}
  Text(stringResource(R.string.reminder_schedule));OutlinedTextField(days,{days=it.filter(Char::isDigit).take(2)},label={Text(stringResource(R.string.days_ahead))},keyboardOptions=KeyboardOptions(keyboardType=KeyboardType.Number))
  OutlinedButton(onClick={days.toIntOrNull()?.let{scope.launch{graph.update(expiryDays=it.coerceIn(0,30))}}}){Text(stringResource(R.string.save))}
  if(android.os.Build.VERSION.SDK_INT>=33)OutlinedButton(onClick={notifications.launch(Manifest.permission.POST_NOTIFICATIONS)}){Text(stringResource(R.string.notification_permission))}
  Text(stringResource(R.string.pending_changes,pending));Text(stringResource(R.string.desktop_features_hint))
  OutlinedButton(onClick=onSignOut){Text(stringResource(R.string.sign_out))}
 }
}

@Composable private fun ConflictDetails(item:Grocery){
 Text(stringResource(if(item.isPurchased)R.string.purchased_status else R.string.remaining_status),style=MaterialTheme.typography.bodySmall)
 Text(stringResource(R.string.expiry_value,date(item.expiresOn)?:stringResource(R.string.unknown_expiry)),style=MaterialTheme.typography.bodySmall)
 Text(stringResource(R.string.required_quantity,Quantities.format(item.requiredQuantity),item.unitAbbreviation),style=MaterialTheme.typography.bodySmall)
 if(item.ingredientId==null)Text(stringResource(R.string.mapping_needed),style=MaterialTheme.typography.bodySmall)
}
