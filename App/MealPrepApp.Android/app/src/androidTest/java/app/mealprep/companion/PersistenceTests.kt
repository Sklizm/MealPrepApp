package app.mealprep.companion

import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.async
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import org.junit.*
import org.junit.Assert.*
import org.junit.runner.RunWith
import java.io.IOException
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class PersistenceTests {
 private lateinit var context:Context
 private lateinit var db:ShoppingDatabase
 private lateinit var vault:SessionVault
 private lateinit var name:String
 private val session=Session("device",42,"Test kitchen","opaque","2099-01-01T00:00:00Z","https://example.test")
 private var remote=Grocery(9,1,1,name="Milk",unitId=1,ingredientId=1,requiredQuantity="200",unitAbbreviation="ml")
 private var offline=true
 private val applied=mutableMapOf<String,MutationAck>()
 private var loseResponse=false
 private var status=0
 private val fake=object:ApiClient(session.baseUrl,session.token){override suspend fun call(method:String,path:String,body:String?):String {
  if(offline)throw IOException("Offline")
  if(status!=0)throw ApiFailure(status)
  if(path=="/v1/sync")return WireJson.encodeToString(SyncSnapshot(1,"2026-11-11T09:00:00Z",listOf(ShoppingList(1,"2026-11-11","2026-11-17","2026-11-11T09:00:00Z")),listOf(remote),emptyList(),emptyList(),emptyList(),emptyList(),emptyList()))
  val op=WireJson.decodeFromString<ShoppingMutation>(body!!)
  val ack=applied.getOrPut(op.operationId){
   if(op.expectedVersion!=remote.version)throw ApiFailure(409)
   remote=remote.copy(version=remote.version+1,isPurchased=op.isPurchased,purchasedQuantity=op.purchasedQuantity)
   MutationAck(remote.itemId,remote.version)
  }
  if(loseResponse){loseResponse=false;throw IOException("Acknowledgement lost")}
  return WireJson.encodeToString(ack)
 }}
 private fun repository(client:ApiClient=fake)=CompanionRepository(session,db,vault,{}, {client})
 @Before fun before(){context=ApplicationProvider.getApplicationContext();name="test-${UUID.randomUUID()}.db";db=Room.databaseBuilder(context,ShoppingDatabase::class.java,name).build();vault=SessionVault(context);vault.save(session)}
 @After fun after(){db.close();context.deleteDatabase(name);vault.clear()}
 @Test fun queuedChecklistAndActualPurchaseSurviveDatabaseReopen()=runBlocking {
  offline=false;repository().sync();offline=true
  val repo=repository();val row=db.dao().serverItem(9)!!;repo.change(row.localId,row.version,remote.copy(isPurchased=true,purchasedQuantity="1000"))
  db.close();db=Room.databaseBuilder(context,ShoppingDatabase::class.java,name).build()
  val restored=db.dao().serverItem(9)!!;val payload=WireJson.decodeFromString<Grocery>(restored.payload)
  assertTrue(payload.isPurchased);assertEquals("1000",payload.purchasedQuantity);assertEquals(1,db.dao().outbox().size)
  offline=false;assertEquals(SyncState.Current,repository().sync());assertTrue(db.dao().outbox().isEmpty());assertEquals("1000",remote.purchasedQuantity)
 }
 @Test fun lostResponseReplaysOriginalRequestAfterDatabaseReopen()=runBlocking {
  offline=false;val repo=repository();repo.sync();val row=db.dao().serverItem(9)!!;repo.change(row.localId,1,remote.copy(isPurchased=true));val id=db.dao().outbox().single().operationId
  loseResponse=true;assertEquals(SyncState.Queued,repo.sync());db.close();db=Room.databaseBuilder(context,ShoppingDatabase::class.java,name).build()
  assertEquals(id,db.dao().outbox().single().operationId);assertEquals(SyncState.Current,repository().sync());assertEquals(1,applied.size);assertEquals(2L,remote.version)
 }
 @Test fun expiredSessionKeepsLocalWork()=runBlocking {
  offline=false;val repo=repository();repo.sync();val row=db.dao().serverItem(9)!!;repo.change(row.localId,1,remote.copy(isPurchased=true));status=401
  assertEquals(SyncState.AuthenticationRequired,repo.sync());assertEquals(1,db.dao().outbox().size);assertTrue(WireJson.decodeFromString<Grocery>(db.dao().item(row.localId)!!.payload).isPurchased)
 }
 @Test fun conflictNeedsExplicitResolutionAndKeepsOtherDeviceValue()=runBlocking {
  offline=false;val repo=repository();repo.sync();val row=db.dao().serverItem(9)!!;repo.change(row.localId,1,remote.copy(purchasedQuantity="1000"));remote=remote.copy(version=2,purchasedQuantity="500")
  assertEquals(SyncState.Conflict,repo.sync());val conflict=db.dao().item(row.localId)!!;assertTrue(conflict.hasConflict)
  assertEquals("1000",WireJson.decodeFromString<Grocery>(conflict.payload).purchasedQuantity);assertEquals("500",WireJson.decodeFromString<Grocery>(conflict.conflictPayload!!).purchasedQuantity)
  repo.resolve(row.localId,2,false);assertTrue(db.dao().outbox().isEmpty());assertEquals("500",WireJson.decodeFromString<Grocery>(db.dao().item(row.localId)!!.payload).purchasedQuantity)
 }
 @Test fun slowNetworkDoesNotBlockImmediateOfflineCheckmark()=runBlocking {
  offline=false;val repo=repository();repo.sync();val row=db.dao().serverItem(9)!!
  val entered=CompletableDeferred<Unit>();val release=CompletableDeferred<Unit>()
  val slow=object:ApiClient(session.baseUrl){override suspend fun call(method:String,path:String,body:String?):String{entered.complete(Unit);release.await();throw IOException("Offline")}}
  val network=repository(slow);val job=async{network.sync()};entered.await()
  kotlinx.coroutines.withTimeout(2000){network.change(row.localId,1,remote.copy(isPurchased=true))}
  assertEquals(1,db.dao().outbox().size);release.complete(Unit);job.await();Unit
 }
 @Test fun accountSwitchCannotReadOrSubmitAnotherAccountsQueue()=runBlocking {
  val repo=repository();repo.addManual(1,"Unmapped groceries");val other=session.copy(userId=43)
  val otherDb=Room.databaseBuilder(context,ShoppingDatabase::class.java,"test-other-$name").build()
  try{assertTrue(otherDb.dao().items().isEmpty());vault.save(other);assertEquals(SyncState.Cached,repo.sync());assertEquals(1,db.dao().outbox().size)}finally{otherDb.close();context.deleteDatabase("test-other-$name")}
 }
 @Test fun reviewedStockRequestIsDurableAndRetriesSameOperation()=runBlocking {
  val ids=mutableListOf<String>();var lost=true
  val stock=object:ApiClient(session.baseUrl){override suspend fun call(method:String,path:String,body:String?):String {
   if(path=="/v1/sync")return fake.call(method,path,body)
   ids+=WireJson.decodeFromString<StockAdjustment>(body!!).operationId
   if(lost){lost=false;throw IOException("Response lost")};return WireJson.encodeToString(MutationAck(3,2))
  }}
  val batch=StockBatch(3,1,1,"Milk","Dairy","ml","1000",null,null,1,"2026-11-11T09:00:00Z")
  try{repository(stock).adjustReviewed(batch,"-100",false);fail("Expected lost response")}catch(expected:IOException){}
  assertEquals(1,db.dao().cache("review").size);db.close();db=Room.databaseBuilder(context,ShoppingDatabase::class.java,name).build();offline=false
  val saved=WireJson.decodeFromString<OnlineReview>(db.dao().cache("review").single().payload);repository(stock).resumeReview(saved)
  assertEquals(2,ids.size);assertEquals(ids[0],ids[1]);assertTrue(db.dao().cache("review").isEmpty())
 }
}
