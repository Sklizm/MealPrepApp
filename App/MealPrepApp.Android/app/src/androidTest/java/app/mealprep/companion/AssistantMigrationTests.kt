package app.mealprep.companion

import androidx.room.testing.MigrationTestHelper
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.*

class AssistantMigrationTests{
 @get:Rule val helper=MigrationTestHelper(InstrumentationRegistry.getInstrumentation(),ShoppingDatabase::class.java)
 @Test fun versionOneUpgradePreservesShoppingCacheAndOutbox(){
  val name="assistant-migration-test"
  helper.createDatabase(name,1).apply{
   execSQL("INSERT INTO shopping_items(localId,serverId,snapshotId,version,payload,hasConflict,conflictPayload) VALUES('local',NULL,1,2,'{}',0,NULL)")
   execSQL("INSERT INTO outbox(sequence,operationId,localId,kind,payload,state) VALUES(1,'stable','local','create','{}','queued')")
   execSQL("INSERT INTO cache(kind,id,payload) VALUES('review','pending','{}')")
   close()
  }
  helper.runMigrationsAndValidate(name,2,true,ASSISTANT_MIGRATION).apply{
   query("SELECT operationId FROM outbox").use{assertTrue(it.moveToFirst());assertEquals("stable",it.getString(0))}
   query("SELECT version FROM shopping_items").use{assertTrue(it.moveToFirst());assertEquals(2,it.getInt(0))}
   query("SELECT payload FROM cache WHERE kind='review'").use{assertTrue(it.moveToFirst());assertEquals("{}",it.getString(0))}
   execSQL("INSERT INTO assistant_state(id,payload) VALUES(1,'{\"prompt\":\"Dinner\"}')")
   query("SELECT payload FROM assistant_state").use{assertTrue(it.moveToFirst());assertTrue(it.getString(0).contains("Dinner"))}
   close()
  }
 }
}
