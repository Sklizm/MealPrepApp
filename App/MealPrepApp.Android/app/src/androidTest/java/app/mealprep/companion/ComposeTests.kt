package app.mealprep.companion
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.assertIsDisplayed
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
@RunWith(AndroidJUnit4::class)
class ComposeTests {
 @get:Rule val compose=createAndroidComposeRule<MainActivity>()
 @Test fun nativeLoginShowsPrivateServerAndAccountFieldsWithoutNotificationPermission(){
  compose.onNodeWithText("Private HTTPS API address").assertIsDisplayed()
  compose.onNodeWithText("Username or email").assertIsDisplayed()
  compose.onNodeWithText("Password").assertIsDisplayed()
 }
}
