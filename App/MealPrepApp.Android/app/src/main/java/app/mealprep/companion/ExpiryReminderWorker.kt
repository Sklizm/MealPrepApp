package app.mealprep.companion

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.serialization.decodeFromString
import java.time.LocalDate
import java.math.BigDecimal

class ExpiryReminderWorker(context:Context,parameters:WorkerParameters):CoroutineWorker(context,parameters){override suspend fun doWork():Result{
 val graph=(applicationContext as MealPrepApplication).graph;val active=graph.session.value?:return Result.success()
 if(active.accountKey!=inputData.getString("account"))return Result.success()
 val settings=graph.settings.filterNotNull().first();val texts=applicationContext.createConfigurationContext(android.content.res.Configuration(applicationContext.resources.configuration).apply{setLocale(java.util.Locale.forLanguageTag(settings.language))});if(!settings.reminders)return Result.success()
 if(Build.VERSION.SDK_INT>=33&&applicationContext.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED)return Result.success()
 val manager=applicationContext.getSystemService(NotificationManager::class.java)
 manager.createNotificationChannel(NotificationChannel("expiry",texts.getString(R.string.expiry_reminders),NotificationManager.IMPORTANCE_DEFAULT))
 if(!NotificationManagerCompat.from(applicationContext).areNotificationsEnabled())return Result.success()
 val today=LocalDate.now();val preferences=applicationContext.getSharedPreferences("reminder_days",Context.MODE_PRIVATE)
 if(preferences.getString(active.accountKey,null)==today.toString())return Result.success()
 val dao=graph.repository()?.dao?:return Result.success()
 val batches=dao.cache("batch").map{WireJson.decodeFromString<StockBatch>(it.payload)}
 val expiring=batches.filter{it.remainingQuantity.toBigDecimal()>BigDecimal.ZERO&&it.expiresOn!=null&&!LocalDate.parse(it.expiresOn).isAfter(today.plusDays(settings.expiryDays.toLong()))}
 val units=dao.cache("unit").map{WireJson.decodeFromString<CatalogUnit>(it.payload)};val mappings=dao.cache("map").map{WireJson.decodeFromString<UnitMapping>(it.payload)};val thresholds=dao.cache("threshold").map{WireJson.decodeFromString<Threshold>(it.payload)}
 val low=pantryGroups(batches,thresholds,units,mappings,today).count{it.low}
 if(expiring.isEmpty()&&low==0)return Result.success()
 val intent=android.content.Intent(applicationContext,MainActivity::class.java).apply{putExtra("pantry",true)}
 val pending=android.app.PendingIntent.getActivity(applicationContext,1,intent,android.app.PendingIntent.FLAG_IMMUTABLE or android.app.PendingIntent.FLAG_UPDATE_CURRENT)
 val text=texts.getString(R.string.reminder_body,expiring.size,low)
 val notification=NotificationCompat.Builder(applicationContext,"expiry").setSmallIcon(R.drawable.ic_notification).setContentTitle(texts.getString(R.string.app_name)).setContentText(text).setStyle(NotificationCompat.BigTextStyle().bigText(text+"\n"+expiring.map{it.ingredientName}.distinct().take(5).joinToString(", "))).setContentIntent(pending).setAutoCancel(true).setVisibility(NotificationCompat.VISIBILITY_PRIVATE).build()
 manager.notify(901,notification);preferences.edit().putString(active.accountKey,today.toString()).apply();return Result.success()
}}
