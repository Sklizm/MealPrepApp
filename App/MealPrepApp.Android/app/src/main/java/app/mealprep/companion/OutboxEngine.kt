package app.mealprep.companion

interface OutboxStore {
 suspend fun pending():List<OutboxEntity>
 suspend fun serverId(localId:String):Int?
 suspend fun attempting(operation:OutboxEntity)
 suspend fun acknowledge(operation:OutboxEntity,result:MutationAck)
 suspend fun conflict(operation:OutboxEntity)
}
fun interface MutationTransport {suspend fun send(operation:OutboxEntity,serverId:Int?):MutationAck}
enum class FlushResult{Complete,Offline,AuthenticationRequired}
/** A request's operation ID and payload remain immutable until an atomic local acknowledgement. */
class OutboxEngine(private val store:OutboxStore,private val transport:MutationTransport){
 suspend fun flush():FlushResult{
  val blocked=mutableSetOf<String>()
  for(operation in store.pending()){
   if(operation.localId in blocked)continue
   try{
    store.attempting(operation)
    val result=transport.send(operation,store.serverId(operation.localId))
    store.acknowledge(operation,result)
   }catch(error:ApiFailure){
    when{
     error.status==401->return FlushResult.AuthenticationRequired
     error.status==429||error.status>=500->return FlushResult.Offline
     else->{store.conflict(operation);blocked+=operation.localId}
    }
   }catch(error:java.io.IOException){return FlushResult.Offline}
  }
  return FlushResult.Complete
 }
}
