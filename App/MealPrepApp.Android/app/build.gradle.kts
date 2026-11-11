plugins {
 alias(libs.plugins.android.application)
 alias(libs.plugins.kotlin.android)
 alias(libs.plugins.kotlin.compose)
 alias(libs.plugins.kotlin.serialization)
 alias(libs.plugins.ksp)
}
android {
 namespace = "app.mealprep.companion"
 compileSdk = 35
 defaultConfig {
  applicationId = "app.mealprep.companion"
  minSdk = 26
  targetSdk = 35
  versionCode = 2
  versionName = "1.0.1"
  testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
 }
 val releaseStore = providers.environmentVariable("MEALPREP_ANDROID_KEYSTORE").orNull
 signingConfigs {
  if (releaseStore != null) create("distribution") {
   storeFile = file(releaseStore)
   storePassword = providers.environmentVariable("MEALPREP_ANDROID_STORE_PASSWORD").orNull
   keyAlias = providers.environmentVariable("MEALPREP_ANDROID_KEY_ALIAS").orNull
   keyPassword = providers.environmentVariable("MEALPREP_ANDROID_KEY_PASSWORD").orNull
  }
 }
 buildTypes {
  release {
   isMinifyEnabled = true
   isShrinkResources = true
   proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
   if (releaseStore != null) signingConfig = signingConfigs.getByName("distribution")
  }
 }
 compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
 kotlinOptions { jvmTarget = "17" }
 buildFeatures { compose = true }
 bundle { language { enableSplit = false } }
 testOptions { unitTests.isReturnDefaultValues = true }
 lint { abortOnError = true }
 sourceSets.getByName("androidTest").assets.srcDir("$projectDir/schemas")
}
ksp { arg("room.schemaLocation", "$projectDir/schemas") }
dependencies {
 implementation(platform(libs.compose.bom))
 implementation(libs.compose.ui)
 implementation(libs.compose.material3)
 implementation(libs.compose.icons)
 implementation(libs.compose.tooling.preview)
 implementation(libs.activity.compose)
 implementation(libs.appcompat)
 implementation(libs.lifecycle.runtime)
 implementation(libs.lifecycle.viewmodel)
 implementation(libs.room.runtime)
 implementation(libs.room.ktx)
 ksp(libs.room.compiler)
 implementation(libs.datastore)
 implementation(libs.coroutines)
 implementation(libs.serialization)
 implementation(libs.work)
 testImplementation(libs.junit)
 testImplementation(libs.coroutines.test)
 androidTestImplementation(libs.android.test)
 androidTestImplementation(libs.android.runner)
 androidTestImplementation(libs.room.testing)
 androidTestImplementation(platform(libs.compose.bom))
 androidTestImplementation(libs.compose.test)
 debugImplementation(libs.compose.tooling)
 debugImplementation(libs.compose.test.manifest)
}
