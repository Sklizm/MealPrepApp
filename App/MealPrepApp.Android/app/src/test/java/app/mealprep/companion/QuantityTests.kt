package app.mealprep.companion
import java.time.LocalDate
import java.util.Locale
import org.junit.Assert.*
import org.junit.Test
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString
class QuantityTests {
 @Test fun decimalTransportNeverUsesBinaryFloatingPoint(){
  val item=Grocery(1,2,3,name="Flour",requiredQuantity="0.1001",purchasedQuantity="1.2345");val result=WireJson.decodeFromString<Grocery>(WireJson.encodeToString(item));assertEquals("1.2345",result.actualQuantity);assertEquals("0.3",(Quantities.decimal("0.1")+Quantities.decimal("0.2")).toPlainString())
 }
 @Test fun validationRejectsUnsupportedPrecisionAndInvalidSigns(){
  for(text in listOf("0","-1","1.00001","NaN","100000000"))assertThrows(IllegalArgumentException::class.java){Quantities.validate(text)}
  assertEquals("1.2345",Quantities.validate("1.234500"));assertEquals("-0.1",Quantities.validate("-0.1",allowNegative=true));assertEquals("0",Quantities.validate("0",allowZero=true))
 }
 @Test fun regionalInputUsesLocaleDecimalSeparator(){val previous=Locale.getDefault();try{Locale.setDefault(Locale.forLanguageTag("ro-RO"));assertEquals("1.25",Quantities.input("1,25"))}finally{Locale.setDefault(previous)}}
 private fun batch(id:Int,unit:Int,quantity:String,expiry:String?)=StockBatch(id,1,unit,"Milk","Dairy","ml",quantity,null,expiry,1,"2026-11-11T09:00:00Z")
 @Test fun compatibleUnitsCombineWhilePiecesRemainSeparateAndExpiredStayVisible(){
  val groups=pantryGroups(listOf(batch(1,1,"200","2026-11-10"),batch(2,2,"1",null),batch(3,1,"100","2026-11-11"),batch(4,3,"2",null)),listOf(Threshold(1,1,"1200",1)),listOf(CatalogUnit(1,"ml","ml","Volume")),listOf(UnitMapping(1,1,"1"),UnitMapping(2,1,"1000")),LocalDate.parse("2026-11-11"))
  assertEquals(2,groups.size);val volume=groups.single{it.unitId==1};assertEquals(0,volume.total.compareTo("1300".toBigDecimal()));assertEquals(0,volume.available.compareTo("1100".toBigDecimal()));assertTrue(volume.low);assertEquals(listOf(1,3,2),volume.batches.map{it.batchId});assertFalse(groups.single{it.unitId==3}.low)
 }
 @Test fun thresholdIsOptInAndUnknownExpiryStaysEligible(){val group=pantryGroups(listOf(batch(1,1,"0.0001",null)),emptyList(),emptyList(),emptyList()).single();assertFalse(group.low);assertEquals("0.0001",group.available.toPlainString())}
 @Test fun credentialsArePartitionedByAccountAndServer(){val a=Session("id",1,"a","token","later","https://example.test");assertNotEquals(a.accountKey,a.copy(userId=2).accountKey);assertNotEquals(a.accountKey,a.copy(baseUrl="https://other.test").accountKey);assertEquals(a.accountKey,a.copy(token="rotated").accountKey)}
 @Test fun privateApiRejectsCleartextAndEmbeddedCredentials(){for(url in listOf("http://example.test","https://user:password@example.test","https://example.test?token=x","https://example.test#fragment"))assertThrows(IllegalArgumentException::class.java){ApiClient(url)}}
}
