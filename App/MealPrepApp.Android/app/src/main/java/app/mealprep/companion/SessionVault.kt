package app.mealprep.companion

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class SessionVault(context:Context){
 private val preferences=context.getSharedPreferences("device_session",Context.MODE_PRIVATE)
 private val alias="MealPrepApp.Companion.DeviceSession.v1"
 private fun key():SecretKey{
  val store=KeyStore.getInstance("AndroidKeyStore").apply{load(null)}
  (store.getKey(alias,null)as? SecretKey)?.let{return it}
  return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore").apply{init(KeyGenParameterSpec.Builder(alias,KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT).setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())}.generateKey()
 }
 @Synchronized fun read():Session?{
  return try{
   val saved=preferences.getString("credential",null)?:return null
   val parts=saved.split(':');if(parts.size!=2)return null
   val cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.DECRYPT_MODE,key(),GCMParameterSpec(128,Base64.decode(parts[0],Base64.NO_WRAP)))
   WireJson.decodeFromString<Session>(cipher.doFinal(Base64.decode(parts[1],Base64.NO_WRAP)).toString(Charsets.UTF_8))
  }catch(error:Exception){null}
 }
 @Synchronized fun save(session:Session){
  val cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,key())
  val saved=Base64.encodeToString(cipher.iv,Base64.NO_WRAP)+":"+Base64.encodeToString(cipher.doFinal(WireJson.encodeToString(session).toByteArray()),Base64.NO_WRAP)
  check(preferences.edit().putString("credential",saved).commit())
 }
 @Synchronized fun clear(){check(preferences.edit().remove("credential").commit());KeyStore.getInstance("AndroidKeyStore").apply{load(null);if(containsAlias(alias))deleteEntry(alias)}}
}
