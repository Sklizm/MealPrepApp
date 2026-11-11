package app.mealprep.companion

import android.app.Application
import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import androidx.room.Room
import androidx.work.*
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import java.util.UUID
import java.time.Duration
import java.time.ZonedDateTime
import java.util.concurrent.TimeUnit

private val Context.preferences by preferencesDataStore(name="preferences")
data class PreferencesState(val language:String="en",val theme:String="system",val reminders:Boolean=true,val expiryDays:Int=3,val lastServer:String="")
class AppGraph(val context:Context){
 val scope=CoroutineScope(SupervisorJob()+Dispatchers.IO)
 val vault=SessionVault(context)
 val session=MutableStateFlow(vault.read())
 val settings:StateFlow<PreferencesState?> = context.preferences.data.map{p->PreferencesState(p[stringPreferencesKey("language")]?:"en",p[stringPreferencesKey("theme")]?:"system",p[booleanPreferencesKey("reminders")]?:true,(p[intPreferencesKey("expiryDays")]?:3).coerceIn(0,30),p[stringPreferencesKey("server")]?:"")}.stateIn(scope,SharingStarted.Eagerly,null)
 private val syncRequests=kotlinx.coroutines.channels.Channel<Unit>(kotlinx.coroutines.channels.Channel.CONFLATED)
 private val repositories=java.util.concurrent.ConcurrentHashMap<String,CompanionRepository>()
 fun repository():CompanionRepository?{val active=session.value?:return null;return repositories.getOrPut(active.accountKey){val db=Room.databaseBuilder(context,ShoppingDatabase::class.java,"companion-${active.accountKey}.db").build();CompanionRepository(active,db,vault,schedule={requestSync()})}}
 suspend fun login(baseUrl:String,identifier:String,password:String){
  val base=baseUrl.trim().trimEnd('/');val result=WireJson.decodeFromString<Session>(ApiClient(base).call("POST","/v1/auth/login",WireJson.encodeToString(LoginRequest(UUID.randomUUID().toString(),identifier.trim(),password,android.os.Build.MODEL.take(80))))).copy(baseUrl=base)
  require(result.protocolVersion==1);vault.save(result);session.value=result
  context.preferences.edit{it[stringPreferencesKey("server")]=base};schedule();requestSync()
 }
 suspend fun update(language:String?=null,theme:String?=null,reminders:Boolean?=null,expiryDays:Int?=null){
  context.preferences.edit{p->if(language!=null){require(language in listOf("en","ro","ru"));p[stringPreferencesKey("language")]=language};if(theme!=null){require(theme in listOf("system","light","dark"));p[stringPreferencesKey("theme")]=theme};if(reminders!=null)p[booleanPreferencesKey("reminders")]=reminders;if(expiryDays!=null)p[intPreferencesKey("expiryDays")]=expiryDays.coerceIn(0,30)};schedule()
 }
 suspend fun selectedList():Int? {val key=session.value?.accountKey?:return null;return context.preferences.data.first()[intPreferencesKey("list-$key")]}
 suspend fun selectList(id:Int){val key=session.value?.accountKey?:return;context.preferences.edit{it[intPreferencesKey("list-$key")]=id}}
 fun requestSync(){val active=session.value?:return;val data=workDataOf("account" to active.accountKey);WorkManager.getInstance(context).enqueueUniqueWork("mealprep-sync-${active.accountKey}",ExistingWorkPolicy.KEEP,OneTimeWorkRequestBuilder<SyncWorker>().setInputData(data).setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()).setBackoffCriteria(BackoffPolicy.EXPONENTIAL,30,TimeUnit.SECONDS).build());syncRequests.trySend(Unit)}
 fun schedule(){val active=session.value?:return;val work=WorkManager.getInstance(context);val data=workDataOf("account" to active.accountKey)
  work.enqueueUniquePeriodicWork("mealprep-periodic-${active.accountKey}",ExistingPeriodicWorkPolicy.UPDATE,PeriodicWorkRequestBuilder<SyncWorker>(15,TimeUnit.MINUTES).setInputData(data).setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()).build())
  val now=ZonedDateTime.now();var next=now.withHour(9).withMinute(0).withSecond(0).withNano(0);if(!next.isAfter(now))next=next.plusDays(1)
  work.enqueueUniquePeriodicWork("mealprep-reminder-${active.accountKey}",ExistingPeriodicWorkPolicy.CANCEL_AND_REENQUEUE,PeriodicWorkRequestBuilder<ExpiryReminderWorker>(1,TimeUnit.DAYS).setInputData(data).setInitialDelay(Duration.between(now,next).toMillis(),TimeUnit.MILLISECONDS).build())
 }
 suspend fun signOut(){val active=session.value?:return;repository()?.signOut();val work=WorkManager.getInstance(context);for(prefix in listOf("mealprep-sync-","mealprep-periodic-","mealprep-reminder-"))work.cancelUniqueWork(prefix+active.accountKey);session.value=null}
 init {scope.launch{for(signal in syncRequests){delay(250);while(syncRequests.tryReceive().isSuccess){};repository()?.sync()}};val manager=context.getSystemService(ConnectivityManager::class.java);manager.registerDefaultNetworkCallback(object:ConnectivityManager.NetworkCallback(){override fun onAvailable(network:Network){requestSync()}});schedule()}
}
class MealPrepApplication:Application(){val graph:AppGraph by lazy{AppGraph(this)};override fun onCreate(){super.onCreate();graph}}
class SyncWorker(context:Context,parameters:WorkerParameters):CoroutineWorker(context,parameters){override suspend fun doWork():Result{
 val graph=(applicationContext as MealPrepApplication).graph;if(graph.session.value?.accountKey!=inputData.getString("account"))return Result.success()
 return try{if(graph.repository()?.sync()==SyncState.Queued)Result.retry()else Result.success()}catch(error:Exception){if(error is CancellationException)throw error;Result.retry()}
}}
class TimeChangeReceiver:android.content.BroadcastReceiver(){override fun onReceive(context:Context,intent:android.content.Intent){if(intent.action in setOf(android.content.Intent.ACTION_TIME_CHANGED,android.content.Intent.ACTION_TIMEZONE_CHANGED))(context.applicationContext as MealPrepApplication).graph.schedule()}}
