package app.mealprep.companion

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import java.net.URI
import java.net.URL
import javax.net.ssl.HttpsURLConnection

class ApiFailure(val status:Int, val code: String? = null):java.io.IOException("HTTP $status")
open class ApiClient(val baseUrl:String,private val token:String?=null) {
 init { val uri=URI(baseUrl);require(uri.scheme=="https"&&uri.host!=null&&uri.userInfo==null&&uri.query==null&&uri.fragment==null) }
 open suspend fun call(method:String,path:String,body:String?=null):String=withContext(Dispatchers.IO) {
  val connection=URL(baseUrl.trimEnd('/')+path).openConnection() as HttpsURLConnection
  connection.requestMethod=method;connection.connectTimeout=12000;connection.readTimeout=if(path=="/v1/recipes/import-preview")25000 else 15000;connection.instanceFollowRedirects=false;connection.useCaches=false
  connection.setRequestProperty("Accept","application/json")
  if(token!=null)connection.setRequestProperty("Authorization","Bearer $token")
  try {
   if(body!=null){connection.doOutput=true;connection.setRequestProperty("Content-Type","application/json; charset=utf-8");val bytes=body.toByteArray(Charsets.UTF_8);require(bytes.size <= when {
    path == "/v1/recipes/save" -> 2 * 1048576
    path == "/v1/ai/jobs" || path.startsWith("/v1/recipes/") || path.startsWith("/v1/ai/jobs/") -> 1048576
    else -> 65536
   });connection.setFixedLengthStreamingMode(bytes.size);connection.outputStream.use{it.write(bytes)}}
   val status=connection.responseCode;if(status !in 200..299)throw failure(connection, status)
   connection.inputStream.use{stream->val result=java.io.ByteArrayOutputStream();val buffer=ByteArray(8192);while(true){val count=stream.read(buffer);if(count<0)break;require(result.size()+count<=16*1024*1024);result.write(buffer,0,count)};result.toString("UTF-8")}
  }finally{connection.disconnect()}
 }
 suspend fun uploadImage(bytes:ByteArray):String=withContext(Dispatchers.IO){
  require(bytes.size in 1..8388608);val connection=URL(baseUrl.trimEnd('/')+"/v1/ai/uploads").openConnection() as HttpsURLConnection
  connection.requestMethod="POST";connection.connectTimeout=12000;connection.readTimeout=30000;connection.instanceFollowRedirects=false;connection.useCaches=false
  connection.setRequestProperty("Authorization","Bearer $token");connection.setRequestProperty("X-Image-Reviewed","true");connection.setRequestProperty("Content-Type","image/jpeg")
  try{connection.doOutput=true;connection.setFixedLengthStreamingMode(bytes.size);connection.outputStream.use{it.write(bytes)};val status=connection.responseCode;if(status !in 200..299)throw failure(connection, status)
   val text=connection.inputStream.bufferedReader().use{it.readText()};require(text.length<4096);kotlinx.serialization.json.Json.parseToJsonElement(text).let{(it as kotlinx.serialization.json.JsonObject)["uploadId"].toString().trim('"')}
  }finally{connection.disconnect()}
 }
 private fun failure(connection: HttpsURLConnection, status: Int): ApiFailure {
  val code = runCatching {
   connection.errorStream?.use { stream ->
    val bytes = ByteArray(4096)
    var count = 0
    while (count < bytes.size) {
     val read = stream.read(bytes, count, bytes.size-count)
     if (read <= 0) break
     count += read
    }
    if (count == 0) null else kotlinx.serialization.json.Json.parseToJsonElement(
     String(bytes, 0, count, Charsets.UTF_8)
    ).let { (it as kotlinx.serialization.json.JsonObject)["errorCode"] as? kotlinx.serialization.json.JsonPrimitive }?.content
   }
  }.getOrNull()
  return ApiFailure(status, code)
 }
 suspend fun sync():SyncSnapshot=WireJson.decodeFromString<SyncSnapshot>(call("GET","/v1/sync")).also{require(it.protocolVersion==1)}
 suspend fun mutate(kind:String,serverId:Int?,payload:String):MutationAck {
  val path=if(kind=="create")"/v1/shopping/items" else "/v1/shopping/items/${requireNotNull(serverId)}"
  return WireJson.decodeFromString(call(if(kind=="create")"POST" else "PUT",path,payload))
 }
 suspend fun revoke(sessionId:String){call("POST","/v1/sessions/$sessionId/revoke",WireJson.encodeToString(Operation(java.util.UUID.randomUUID().toString())))}
}
