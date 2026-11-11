package app.mealprep.companion

import androidx.room.withTransaction
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import java.time.LocalDate
import java.util.UUID

enum class SyncState{Cached,Syncing,Current,Queued,AuthenticationRequired,Conflict}
class LocalVersionConflict:IllegalStateException("Review the updated item before changing it.")
class CompanionRepository(val session:Session,val database:ShoppingDatabase,private val vault:SessionVault,private val schedule:()->Unit,private val client:(Session)->ApiClient={ApiClient(it.baseUrl,it.token)}){
 val dao=database.dao();val state=MutableStateFlow(SyncState.Cached);private val gate=Mutex();private val localGate=Mutex()
 private fun api():ApiClient{val current=vault.read();check(current?.accountKey==session.accountKey);return client(current!!)}
 private fun active()=vault.read()?.accountKey==session.accountKey
 val items=dao.observeItems();val outbox=dao.observeOutbox();val cache=dao.observeCache()
 suspend fun addManual(snapshotId:Int,name:String){localGate.withLock{
  require(name.isNotBlank()&&name.length<=150);check(active());val id=UUID.randomUUID().toString();val operation=UUID.randomUUID().toString()
  val item=Grocery(0,snapshotId,1,name=name.trim(),isManual=true)
  database.withTransaction{dao.putItem(ItemEntity(id,null,snapshotId,1,WireJson.encodeToString(item)));dao.enqueue(OutboxEntity(operationId=operation,localId=id,kind="create",payload=WireJson.encodeToString(item.change(operation,0))))}
  state.value=SyncState.Queued
 };schedule()}
 suspend fun change(localId:String,observedVersion:Long,desired:Grocery){localGate.withLock{
  check(active());database.withTransaction{
   val old=dao.item(localId)?:throw LocalVersionConflict();if(old.hasConflict||old.version!=observedVersion)throw LocalVersionConflict()
   val operation=UUID.randomUUID().toString();val next=desired.copy(itemId=old.serverId?:0,version=old.version+1)
   dao.enqueue(OutboxEntity(operationId=operation,localId=localId,kind="update",payload=WireJson.encodeToString(next.change(operation,old.version))))
   dao.putItem(old.copy(version=next.version,payload=WireJson.encodeToString(next)))
  };state.value=SyncState.Queued
 };schedule()}
 private val store=object:OutboxStore{
  override suspend fun pending()=dao.pending()
  override suspend fun serverId(localId:String)=dao.item(localId)?.serverId
  override suspend fun attempting(operation:OutboxEntity)=dao.setState(operation.sequence,"sending")
  override suspend fun acknowledge(operation:OutboxEntity,result:MutationAck){database.withTransaction{
   val row=dao.item(operation.localId)?:return@withTransaction
   dao.acknowledge(operation.sequence);val hasMore=dao.outbox().any{it.localId==row.localId};val version=if(hasMore)row.version else result.version
   val payload=WireJson.decodeFromString<Grocery>(row.payload).copy(itemId=result.id,version=version)
   dao.putItem(row.copy(serverId=result.id,version=version,payload=WireJson.encodeToString(payload)))
  }}
  override suspend fun conflict(operation:OutboxEntity){database.withTransaction{dao.block(operation.localId);dao.item(operation.localId)?.let{dao.putItem(it.copy(hasConflict=true))}}}
 }
 private suspend fun pull():SyncSnapshot{
  val snapshot=api().sync();check(active())
  database.withTransaction{
   val operations=dao.outbox();val pendingIds=operations.map{it.localId}.toSet()
   for(remote in snapshot.items){
    val previous=dao.serverItem(remote.itemId)
    if(previous==null)dao.putItem(ItemEntity("server-${remote.itemId}",remote.itemId,remote.snapshotId,remote.version,WireJson.encodeToString(remote)))
    else if(previous.localId !in pendingIds)dao.putItem(previous.copy(version=remote.version,payload=WireJson.encodeToString(remote),hasConflict=false,conflictPayload=null))
    else if(previous.hasConflict)dao.putItem(previous.copy(conflictPayload=WireJson.encodeToString(remote)))
   }
   val remoteIds=snapshot.items.map{it.itemId}.toSet()
   for(local in dao.items())if(local.serverId!=null&&local.serverId !in remoteIds&&local.localId !in pendingIds)dao.removeItem(local.localId)
   dao.clearCache();val values=mutableListOf<CacheEntity>()
   values+=snapshot.lists.map{CacheEntity("list",it.snapshotId.toString(),WireJson.encodeToString(it))}
   values+=snapshot.batches.map{CacheEntity("batch",it.batchId.toString(),WireJson.encodeToString(it))}
   values+=snapshot.ingredients.map{CacheEntity("ingredient",it.ingredientId.toString(),WireJson.encodeToString(it))}
   values+=snapshot.units.map{CacheEntity("unit",it.unitId.toString(),WireJson.encodeToString(it))}
   values+=snapshot.unitMap.map{CacheEntity("map",it.unitId.toString(),WireJson.encodeToString(it))}
   values+=snapshot.thresholds.map{CacheEntity("threshold","${it.ingredientId}:${it.unitId}",WireJson.encodeToString(it))}
   values+=CacheEntity("meta","sync",snapshot.serverUtcNow);dao.putCache(values)
  };return snapshot
 }
 suspend fun sync():SyncState=gate.withLock{
  if(!active())return@withLock SyncState.Cached
  state.value=SyncState.Syncing
  try{
   val result=OutboxEngine(store,MutationTransport{operation,id->api().mutate(operation.kind,id,operation.payload)}).flush()
   if(result==FlushResult.AuthenticationRequired){state.value=SyncState.AuthenticationRequired;return@withLock state.value}
   if(result==FlushResult.Offline){state.value=SyncState.Queued;return@withLock state.value}
   pull();state.value=if(dao.items().any{it.hasConflict})SyncState.Conflict else if(dao.outbox().isEmpty())SyncState.Current else SyncState.Queued
  }catch(error:ApiFailure){state.value=if(error.status==401)SyncState.AuthenticationRequired else SyncState.Queued}
  catch(error:java.io.IOException){state.value=SyncState.Queued}
  catch(error:Exception){if(error is kotlinx.coroutines.CancellationException)throw error;state.value=SyncState.Queued}
  state.value
 }
 suspend fun resolve(localId:String,observedRemoteVersion:Long,keepMine:Boolean){localGate.withLock{
  database.withTransaction{
   val row=dao.item(localId)?:throw LocalVersionConflict();val remote=row.conflictPayload?.let{WireJson.decodeFromString<Grocery>(it)}?:throw LocalVersionConflict()
   if(!row.hasConflict||remote.version!=observedRemoteVersion)throw LocalVersionConflict()
   val desired=WireJson.decodeFromString<Grocery>(row.payload);dao.clearOperations(localId)
   if(!keepMine)dao.putItem(row.copy(version=remote.version,payload=WireJson.encodeToString(remote),hasConflict=false,conflictPayload=null))
   else{
    val operation=UUID.randomUUID().toString();val create=remote.isDeleted;val next=desired.copy(itemId=if(create)0 else remote.itemId,version=if(create)1 else remote.version+1,isDeleted=if(create)false else desired.isDeleted)
    dao.putItem(row.copy(serverId=if(create)null else remote.itemId,version=next.version,payload=WireJson.encodeToString(next),hasConflict=false,conflictPayload=null))
    dao.enqueue(OutboxEntity(operationId=operation,localId=localId,kind=if(create)"create" else "update",payload=WireJson.encodeToString(next.change(operation,if(create)0 else remote.version))))
   }
  }
 };schedule()}
 suspend fun reviewPurchase(localId:String):Grocery=gate.withLock{
  check(active());check(dao.outbox().none{it.localId==localId});val fresh=pull();check(dao.outbox().none{it.localId==localId});val id=dao.item(localId)?.serverId?:throw LocalVersionConflict();fresh.items.single{it.itemId==id}.also{require(it.isPurchased&&!it.isImported&&it.ingredientId!=null&&(it.purchasedUnitId?:it.unitId)!=null)}
 }
 private suspend fun online(subject:String,method:String,path:String,summary:String,body:(String)->String):MutationAck {
  check(active());val old=dao.cache("review").firstOrNull{it.id==subject}?.let{WireJson.decodeFromString<OnlineReview>(it.payload)}
  val operation=UUID.randomUUID().toString();val payload=body(operation)
  if(old!=null)throw PendingReviewRequired()
  val review=OnlineReview(subject,method,path,payload,summary)
  dao.putCache(listOf(CacheEntity("review",subject,WireJson.encodeToString(review))))
  return finishReview(review)
 }
 private suspend fun finishReview(review:OnlineReview):MutationAck {
  check(active())
  val result=try{WireJson.decodeFromString<MutationAck>(api().call(review.method,review.path,review.payload))}
  catch(error:ApiFailure){if(error.status in 400..499&&error.status!=401&&error.status!=429)dao.clearReview(review.subject);throw error}
  dao.clearReview(review.subject);pull();return result
 }
 suspend fun resumeReview(review:OnlineReview):MutationAck=gate.withLock{
  val saved=dao.cache("review").firstOrNull{it.id==review.subject}?:throw LocalVersionConflict()
  check(saved.payload==WireJson.encodeToString(review));finishReview(review)
 }
 suspend fun importReviewed(item:Grocery){gate.withLock{online("import:${item.itemId}","POST","/v1/shopping/items/${item.itemId}/import","${item.name} · ${item.actualQuantity} ${item.actualUnit}"){WireJson.encodeToString(Versioned(it,item.version))}}}
 suspend fun reviewBatch(batchId:Int):StockBatch=gate.withLock{pull().batches.single{it.batchId==batchId}}
 suspend fun adjustReviewed(batch:StockBatch,delta:String,discard:Boolean){gate.withLock{val amount=Quantities.validate(delta,allowNegative=true);online("stock:${batch.batchId}","POST","/v1/pantry/batches/${batch.batchId}/adjust","${batch.ingredientName} · $amount ${batch.unitAbbreviation}"){WireJson.encodeToString(StockAdjustment(it,batch.version,amount,if(discard)"Discard" else "Correction"))}}}
 suspend fun reviewThreshold(ingredientId:Int,unitId:Int):Threshold=gate.withLock{pull().thresholds.firstOrNull{it.ingredientId==ingredientId&&it.unitId==unitId}?:Threshold(ingredientId,unitId,"0",0)}
 suspend fun setThreshold(reviewed:Threshold,quantity:String){gate.withLock{val amount=Quantities.validate(quantity,allowZero=true);online("threshold:${reviewed.ingredientId}","PUT","/v1/pantry/thresholds/${reviewed.ingredientId}","$amount"){WireJson.encodeToString(ThresholdChange(it,reviewed.version,reviewed.unitId,amount))}}}
 suspend fun createList():Int=gate.withLock{val today=LocalDate.now();online("list","POST","/v1/shopping/lists","$today – ${today.plusDays(6)}"){WireJson.encodeToString(CreateList(it,today.toString(),today.plusDays(6).toString()))}.id}
 suspend fun signOut(){gate.withLock{
  val current=vault.read();check(current?.accountKey==session.accountKey)
  try{api().revoke(current!!.sessionId)}catch(error:java.io.IOException){}
  localGate.withLock{database.withTransaction{dao.clearOutbox();dao.clearItems();dao.clearAllCache()};vault.clear()}
 }}
}
