package app.mealprep.companion

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.math.BigDecimal
import java.math.RoundingMode
import java.time.LocalDate

val WireJson = Json { ignoreUnknownKeys = true; encodeDefaults = true }
@Serializable data class Session(val sessionId:String,val userId:Int,val username:String,val token:String,val expiresAt:String,val baseUrl:String="",val protocolVersion:Int=1) {
 val accountKey:String get() = java.security.MessageDigest.getInstance("SHA-256").digest("$baseUrl|$userId".toByteArray()).joinToString(""){"%02x".format(it)}.take(24)
}
@Serializable data class LoginRequest(val operationId:String,val identifier:String,val password:String,val deviceName:String)
@Serializable data class Operation(val operationId:String)
@Serializable data class Versioned(val operationId:String,val expectedVersion:Long)
@Serializable data class MutationAck(val id:Int,val version:Long,val eventId:Int?=null)
@Serializable data class ShoppingList(val snapshotId:Int,val startDate:String,val endDate:String,val createdAt:String)
@Serializable data class Grocery(val itemId:Int,val snapshotId:Int,val version:Long,val ingredientId:Int?=null,val unitId:Int?=null,val name:String,val categoryName:String="Manual",val unitAbbreviation:String="",val neededQuantity:String="0",val onHandQuantity:String="0",val requiredQuantity:String="1",val isPurchased:Boolean=false,val purchasedQuantity:String?=null,val purchasedUnitId:Int?=null,val purchasedUnitAbbreviation:String="",val purchasedOn:String?=null,val expiresOn:String?=null,val isManual:Boolean=false,val isImported:Boolean=false,val isDeleted:Boolean=false) {
 val actualQuantity:String get()=purchasedQuantity?:requiredQuantity
 val actualUnit:String get()=purchasedUnitAbbreviation.ifEmpty { unitAbbreviation }
}
@Serializable data class ShoppingMutation(val operationId:String,val expectedVersion:Long,val snapshotId:Int,val name:String,val ingredientId:Int?,val unitId:Int?,val requiredQuantity:String,val isPurchased:Boolean,val purchasedQuantity:String?,val purchasedUnitId:Int?,val purchasedOn:String?,val expiresOn:String?,val isDeleted:Boolean=false)
fun Grocery.change(operationId:String,expectedVersion:Long)=ShoppingMutation(operationId,expectedVersion,snapshotId,name,ingredientId,unitId,requiredQuantity,isPurchased,purchasedQuantity,purchasedUnitId,purchasedOn,expiresOn,isDeleted)
@Serializable data class StockBatch(val batchId:Int,val ingredientId:Int,val unitId:Int,val ingredientName:String,val categoryName:String,val unitAbbreviation:String,val remainingQuantity:String,val purchasedOn:String?,val expiresOn:String?,val version:Long,val updatedAt:String)
@Serializable data class Threshold(val ingredientId:Int,val unitId:Int,val quantity:String,val version:Long)
@Serializable data class CatalogIngredient(val ingredientId:Int,val name:String,val defaultUnitId:Int?,val categoryName:String?)
@Serializable data class CatalogUnit(val unitId:Int,val name:String,val abbreviation:String,val unitType:String)
@Serializable data class UnitMapping(val unitId:Int,val canonicalUnitId:Int,val multiplier:String)
@Serializable data class SyncSnapshot(val protocolVersion:Int,val serverUtcNow:String,val lists:List<ShoppingList>,val items:List<Grocery>,val batches:List<StockBatch>,val thresholds:List<Threshold>,val ingredients:List<CatalogIngredient>,val units:List<CatalogUnit>,val unitMap:List<UnitMapping>)
@Serializable data class StockAdjustment(val operationId:String,val expectedVersion:Long,val delta:String,val kind:String="Correction")
@Serializable data class ThresholdChange(val operationId:String,val expectedVersion:Long,val unitId:Int,val quantity:String)
@Serializable data class CreateList(val operationId:String,val startDate:String,val endDate:String)

object Quantities {
 fun decimal(text:String):BigDecimal = text.toBigDecimal()
 fun validate(text:String,allowZero:Boolean=false,allowNegative:Boolean=false):String {
  val value=text.toBigDecimal();require(value.scale()<=4 || value.stripTrailingZeros().scale()<=4)
  require(value.abs()<=BigDecimal("99999999"));require(if(allowNegative)value.compareTo(BigDecimal.ZERO)!=0 else if(allowZero)value>=BigDecimal.ZERO else value>BigDecimal.ZERO)
  return value.stripTrailingZeros().toPlainString()
 }
 fun input(text:String,allowZero:Boolean=false,allowNegative:Boolean=false):String {
  val separator=java.text.DecimalFormatSymbols(java.util.Locale.getDefault()).decimalSeparator
  val normalized=text.trim().let { if(separator==',')it.replace(',','.') else it }
  return validate(normalized,allowZero,allowNegative)
 }
 fun format(text:String):String = java.text.NumberFormat.getNumberInstance().apply{maximumFractionDigits=4}.format(text.toBigDecimal())
}
data class PantryGroup(val ingredientId:Int,val unitId:Int,val name:String,val unit:String,val total:BigDecimal,val available:BigDecimal,val batches:List<StockBatch>,val low:Boolean)
fun pantryGroups(batches:List<StockBatch>,thresholds:List<Threshold>,units:List<CatalogUnit>,mappings:List<UnitMapping>,today:LocalDate=LocalDate.now()):List<PantryGroup> {
 val maps=mappings.associateBy{it.unitId};fun canonical(id:Int)=maps[id]?.canonicalUnitId?:id
 fun factor(id:Int)=maps[id]?.multiplier?.toBigDecimal()?:BigDecimal.ONE
 return batches.filter{it.remainingQuantity.toBigDecimal()>BigDecimal.ZERO}.groupBy{it.ingredientId to canonical(it.unitId)}.map{(key,rows)->
  val available=rows.filter{it.expiresOn==null||!LocalDate.parse(it.expiresOn).isBefore(today)}.fold(BigDecimal.ZERO){sum,b->sum+b.remainingQuantity.toBigDecimal()*factor(b.unitId)}
  PantryGroup(key.first,key.second,rows.first().ingredientName,units.find{it.unitId==key.second}?.abbreviation?:rows.first().unitAbbreviation,rows.fold(BigDecimal.ZERO){sum,b->sum+b.remainingQuantity.toBigDecimal()*factor(b.unitId)},available,rows.sortedWith(compareBy<StockBatch>{it.expiresOn==null}.thenBy{it.expiresOn}.thenBy{it.batchId}),thresholds.any{it.ingredientId==key.first&&canonical(it.unitId)==key.second&&it.quantity.toBigDecimal()>BigDecimal.ZERO&&available<it.quantity.toBigDecimal()*factor(it.unitId)})
 }.sortedBy{it.name}
}

/** Confirmed online mutations survive a lost response without silently creating a second operation. */
@Serializable data class OnlineReview(val subject:String,val method:String,val path:String,val payload:String,val summary:String)
class PendingReviewRequired:IllegalStateException("Complete the pending review first.")
