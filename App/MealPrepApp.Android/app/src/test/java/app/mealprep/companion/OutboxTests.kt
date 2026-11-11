package app.mealprep.companion
import java.io.IOException
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test
class OutboxTests {
 private class Store(val rows:MutableList<OutboxEntity>):OutboxStore {
  val ids=mutableMapOf<String,Int>();val blocked=mutableSetOf<String>();val attempts=mutableListOf<String>()
  override suspend fun pending()=rows.filter{it.localId !in blocked}.toList()
  override suspend fun serverId(localId:String)=ids[localId]
  override suspend fun attempting(operation:OutboxEntity){attempts+=operation.operationId}
  override suspend fun acknowledge(operation:OutboxEntity,result:MutationAck){ids[operation.localId]=result.id;rows.remove(operation)}
  override suspend fun conflict(operation:OutboxEntity){blocked+=operation.localId}
 }
 private fun row(sequence:Long=1,id:String="local",kind:String="update")=OutboxEntity(sequence,"op-$sequence",id,kind,"{immutable}")
 @Test fun lostResponseRetriesIdenticalOperationAfterRestart()=runTest {
  val original=row();val disk=mutableListOf(original);val saved=Store(disk);var applied=0;val server=mutableMapOf<String,MutationAck>()
  val first=OutboxEngine(saved,MutationTransport { op,_->server.getOrPut(op.operationId){applied++;MutationAck(42,8)};throw IOException("Response lost") })
  assertEquals(FlushResult.Offline,first.flush());assertEquals(listOf(original),disk)
  val second=OutboxEngine(Store(disk),MutationTransport { op,_->assertEquals(original,op);server.getValue(op.operationId) })
  assertEquals(FlushResult.Complete,second.flush());assertTrue(disk.isEmpty());assertEquals(1,applied)
 }
 @Test fun createThenEditsResolveNewServerIdInOrder()=runTest {
  val store=Store(mutableListOf(row(1,kind="create"),row(2),row(3)));val observed=mutableListOf<Int?>()
  OutboxEngine(store,MutationTransport{op,id->observed+=id;MutationAck(31,op.sequence)}).flush()
  assertEquals(listOf(null,31,31),observed);assertTrue(store.rows.isEmpty())
 }
 @Test fun expiredSessionRetainsEveryRequest()=runTest {
  val store=Store(mutableListOf(row(1),row(2)));assertEquals(FlushResult.AuthenticationRequired,OutboxEngine(store,MutationTransport{_,_->throw ApiFailure(401)}).flush());assertEquals(2,store.rows.size);assertTrue(store.blocked.isEmpty())
 }
 @Test fun conflictBlocksThatItemButContinuesOthers()=runTest {
  val store=Store(mutableListOf(row(1),row(2),row(3,"other")));val sent=mutableListOf<String>()
  OutboxEngine(store,MutationTransport{op,_->sent+=op.operationId;if(op.localId=="local")throw ApiFailure(409);MutationAck(90,1)}).flush()
  assertEquals(listOf("op-1","op-3"),sent);assertEquals(2,store.rows.size);assertEquals(setOf("local"),store.blocked)
 }
 @Test fun transientFailuresRetainTheQueueForRetry()=runTest {
  for(code in listOf(429,500,503)){val store=Store(mutableListOf(row()));assertEquals(FlushResult.Offline,OutboxEngine(store,MutationTransport{_,_->throw ApiFailure(code)}).flush());assertEquals(1,store.rows.size);assertTrue(store.blocked.isEmpty())}
 }
 @Test fun rejectedRequestRequiresResolutionWithoutLosingPayload()=runTest {
  val original=row();val store=Store(mutableListOf(original));OutboxEngine(store,MutationTransport{_,_->throw ApiFailure(400)}).flush();assertEquals(listOf(original),store.rows);assertEquals(setOf("local"),store.blocked)
 }
}
