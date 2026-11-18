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
import org.junit.Assert.assertTrue
import org.junit.runner.RunWith
import java.io.File

@RunWith(AndroidJUnit4::class)
class VisualTests {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 @Test fun navigationRemainsAlignedInEveryLanguageAndTheme() {
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
    val destinations = listOf(
     "shopping" to R.string.shopping, "pantry" to R.string.pantry,
     "recipes" to R.string.nav_recipes, "plan" to R.string.nav_plan, "settings" to R.string.settings
    )
    val inNavigation = hasAnyAncestor(hasTestTag("companion-navigation"))
    for ((page, label) in destinations) {
     val text = compose.activity.getString(label)
     compose.onNode(hasText(text) and inNavigation).performClick().assertIsSelected()
     compose.waitForIdle()
     val labels = destinations.map { (_, id) ->
      compose.onNode(hasText(compose.activity.getString(id)) and inNavigation, useUnmergedTree = true)
       .fetchSemanticsNode().boundsInRoot.top
     }
     val icons = destinations.indices.map { index ->
      compose.onNodeWithTag("companion-nav-icon-$index", useUnmergedTree = true)
       .fetchSemanticsNode().boundsInRoot.top
     }
     assertTrue("Navigation labels have different baselines: $labels", labels.max() - labels.min() <= 1f)
     assertTrue("Navigation icons have different top edges: $icons", icons.max() - icons.min() <= 1f)
     val bitmap=InstrumentationRegistry.getInstrumentation().uiAutomation.takeScreenshot();try{File(target,"$page-$language-$theme.png").outputStream().use{bitmap.compress(Bitmap.CompressFormat.PNG,100,it)}}finally{bitmap.recycle()}
    }
   }
  }finally{runBlocking{graph.update(language="en",theme="system");graph.repository()?.dao?.let{it.clearItems();it.clearAllCache();it.clearOutbox()};graph.vault.clear();graph.session.value=null}}
 }
}
