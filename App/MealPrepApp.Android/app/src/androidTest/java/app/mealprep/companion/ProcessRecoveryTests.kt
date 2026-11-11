package app.mealprep.companion
import android.content.Context
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.decodeFromString
import org.junit.Test
import org.junit.Assert.*
import org.junit.runner.RunWith
import java.io.IOException
@RunWith(AndroidJUnit4::class)
class ProcessRecoveryTests {
 /** Invoked separately with a force-stop and airplane-mode transition between phases. */
 @Test fun processRecovery()=runBlocking {
  val context=ApplicationProvider.getApplicationContext<Context>();val args=InstrumentationRegistry.getArguments();val phase=args.getString("recoveryPhase")?:return@runBlocking
  val name="process-recovery-qa.db";val db=Room.databaseBuilder(context,ShoppingDatabase::class.java,name).build();val vault=SessionVault(context)
  try {
   val session=Session("recovery",700,"Test kitchen","opaque","2099-01-01T00:00:00Z","https://example.test")
   val offline=object:ApiClient(session.baseUrl){override suspend fun call(method:String,path:String,body:String?):String{throw IOException("Offline")}}
   if(phase=="seed"){
    vault.save(session);val repo=CompanionRepository(session,db,vault,{}, {offline});repo.addManual(1,"Offline purchase");val row=db.dao().items().single()
    repo.change(row.localId,row.version,WireJson.decodeFromString<Grocery>(row.payload).copy(isPurchased=true,purchasedQuantity="1.2345"))
    assertEquals(2,db.dao().outbox().size)
   }else{
    assertEquals(session.accountKey,vault.read()!!.accountKey);assertEquals(2,db.dao().outbox().size)
    val restored=WireJson.decodeFromString<Grocery>(db.dao().items().single().payload);assertTrue(restored.isPurchased);assertEquals("1.2345",restored.purchasedQuantity)
    val repo=CompanionRepository(session,db,vault,{}, {offline});assertEquals(SyncState.Queued,repo.sync());assertEquals(2,db.dao().outbox().size)
    vault.clear()
   }
  }finally{db.close();if(phase!="seed")context.deleteDatabase(name)}
 }
 @Test fun privateHttpsHealthAndAnonymousAuthentication()=runBlocking {
  val url=InstrumentationRegistry.getArguments().getString("httpsUrl")?:return@runBlocking
  assertTrue(ApiClient(url).call("GET","/health").contains("protocolVersion"))
  try{ApiClient(url).call("GET","/v1/sync");fail("Authentication is required")}catch(error:ApiFailure){assertEquals(401,error.status)}
 }
}
