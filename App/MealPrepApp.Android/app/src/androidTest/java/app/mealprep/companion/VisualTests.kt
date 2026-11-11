package app.mealprep.companion

import android.content.Context
import android.graphics.Bitmap
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.*
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.encodeToString
import org.junit.*
import org.junit.runner.RunWith
import java.io.File

@RunWith(AndroidJUnit4::class)
class VisualTests {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 @Test fun shoppingPantryAndSettingsInEveryLanguageAndTheme() {
  val app=ApplicationProvider.getApplicationContext<MealPrepApplication>();val graph=app.graph
  val session=Session("visual",990,"Test kitchen","fixture","2099-01-01T00:00:00Z","https://example.test")
  runBlocking {
   graph.vault.save(session);graph.session.value=session;val repo=graph.repository()!!;val dao=repo.dao
   dao.clearItems();dao.clearOutbox();dao.clearAllCache()
   val item=Grocery(1,1,1,name="Tomatoes",ingredientId=1,unitId=1,requiredQuantity="500",unitAbbreviation="g")
   dao.putItem(ItemEntity("server-1",1,1,1,WireJson.encodeToString(item)))
   dao.putCache(listOf(CacheEntity("list","1",WireJson.encodeToString(ShoppingList(1,"2026-11-09","2026-11-15","2026-11-09T09:00:00Z"))),CacheEntity("unit","1",WireJson.encodeToString(CatalogUnit(1,"grams","g","Mass"))),CacheEntity("ingredient","1",WireJson.encodeToString(CatalogIngredient(1,"Tomatoes",1,"Vegetables"))),CacheEntity("batch","1",WireJson.encodeToString(StockBatch(1,1,1,"Tomatoes","Vegetables","g","350",null,"2026-11-13",1,"2026-11-11T09:00:00Z")))))
  }
  val target=File(app.getExternalFilesDir(null),"qa").apply{mkdirs()}
  try {
   for(language in listOf("en","ro","ru"))for(theme in listOf("light","dark")){
    runBlocking{graph.update(language=language,theme=theme)}
    compose.waitForIdle()
    for((page,label)in listOf("shopping" to R.string.shopping,"pantry" to R.string.pantry,"settings" to R.string.settings)){
     val text=compose.activity.getString(label)
     compose.onAllNodesWithText(text).onLast().performClick();compose.waitForIdle()
     val bitmap=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot();try{File(target,"$page-$language-$theme.png").outputStream().use{bitmap.compress(Bitmap.CompressFormat.PNG,100,it)}}finally{bitmap.recycle()}
    }
   }
  }finally{runBlocking{graph.update(language="en",theme="system");graph.repository()?.dao?.let{it.clearItems();it.clearAllCache();it.clearOutbox()};graph.vault.clear();graph.session.value=null}}
 }
}
