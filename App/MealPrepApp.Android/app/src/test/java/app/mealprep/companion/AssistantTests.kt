package app.mealprep.companion

import org.junit.Assert.*
import org.junit.Test
import kotlinx.serialization.encodeToString
import kotlinx.serialization.decodeFromString

class AssistantTests{
 @Test fun exactDecimalsAcceptApiStringsAndDurableNumbers(){
  val fromNumber=WireJson.decodeFromString<ALine>("""{"name":"Rice","quantity":0.125}""")
  val fromString=WireJson.decodeFromString<ALine>("""{"name":"Rice","quantity":"0.125"}""")
  assertEquals("0.125",fromNumber.quantity);assertEquals(fromNumber,fromString)
 }
 @Test fun proposalUnknownMappingsStayUnresolved(){val result=WireJson.decodeFromString<AProposal>("""{"recipes":[{"title":"Recipe","instructions":"Cook","ingredients":[{"original":"a little rice","name":"Rice","quantity":null,"ingredientId":null,"unitId":null}]}]}""");assertNull(result.recipes[0].ingredients[0].quantity);assertNull(result.recipes[0].ingredients[0].unitId)}
 @Test fun pendingOperationAndRecipeIdentitySurviveProcessRecovery(){
  val r=ARecipeDraft(ARecipe(title="Rice"),recipeId=42,version="AAAAAAAB",publishedId=42)
  val pending=APending("/v1/recipes/save","{\"operationId\":\"stable\"}","recipe",0)
  val w=AWorkspace(recipes=listOf(r),pending=pending,pinned=listOf(0),replaceIds=listOf(4))
  val copy=WireJson.decodeFromString<AWorkspace>(WireJson.encodeToString(w))
  assertEquals(w,copy);assertEquals(r.operationId,copy.recipes[0].operationId);assertEquals(pending,copy.pending)
 }
 @Test fun apiAcronymFieldsMapToAndroidIdentifiers(){
  val ingredient=WireJson.decodeFromString<AIngredient>("""{"ingredientID":8,"name":"Rice","defaultUnitID":1}""")
  val slot=WireJson.decodeFromString<ASlot>("""{"categoryID":2,"name":"Dinner"}""")
  assertEquals(8,ingredient.ingredientId);assertEquals(2,slot.categoryId)
 }
 @Test fun newDraftReferencesAndSharedCookingGroupsRemainDistinct(){val meals=listOf(AMeal("2030-04-01",1,draftIndex=0,portions="1",batchKey="shared"),AMeal("2030-04-02",1,draftIndex=0,portions="1",batchKey="shared"));val copy=WireJson.decodeFromString<AProposal>(WireJson.encodeToString(AProposal(meals=meals)));assertEquals(1,copy.meals.map{it.batchKey}.distinct().size);assertNull(copy.meals[0].recipeId);assertEquals(0,copy.meals[0].draftIndex)}
 @Test fun receiptSelectionAndUnknownExpirySurviveRecovery(){val w=AWorkspace(purchases=listOf(APurchase("Rice 2 kg","Rice",1,"2",2,"kg","2030-04-01",null)),selectedPurchases=listOf(0));val copy=WireJson.decodeFromString<AWorkspace>(WireJson.encodeToString(w));assertNull(copy.purchases[0].expiresOn);assertEquals(listOf(0),copy.selectedPurchases)}
}
