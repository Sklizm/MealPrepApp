package app.mealprep.companion

import kotlinx.serialization.*
import kotlinx.serialization.descriptors.*
import kotlinx.serialization.encoding.*
import kotlinx.serialization.json.*
import java.time.LocalDate
import java.util.UUID

/** The API transports decimals as strings; durable proposal JSON also accepts JSON numbers. */
object AssistantDecimal:KSerializer<String>{
 override val descriptor=PrimitiveSerialDescriptor("AssistantDecimal",PrimitiveKind.STRING)
 override fun serialize(encoder:Encoder,value:String)=encoder.encodeString(value)
 override fun deserialize(decoder:Decoder):String=if(decoder is JsonDecoder)decoder.decodeJsonElement().jsonPrimitive.content else decoder.decodeString()
}
@Serializable data class APreferences(@Serializable(with=AssistantDecimal::class)val servings:String="2",val maxMinutes:Int=45,val excludedIngredientIds:List<Int> = emptyList(),val mealSlotIds:List<Int>?=null,val preferExpiring:Boolean=true,val useLeftovers:Boolean=true,val maxRepetitions:Int=2)
@Serializable data class ARequest(val operationId:String=UUID.randomUUID().toString(),val kind:String,val language:String,val prompt:String,val startDate:String,val preferences:APreferences,val source:String?=null,val uploadId:String?=null,val allowNewRecipes:Boolean=false,val replaceEntryIds:List<Int> = emptyList(),val pinnedMeals:List<AMeal> = emptyList())
@Serializable data class AIngredient(@SerialName("ingredientID")val ingredientId:Int,val name:String,val defaultUnitID:Int?=null)
@Serializable data class AUnit(@SerialName("unitID")val unitId:Int,val name:String,val abbreviation:String,val unitType:String="")
@Serializable data class ASlot(@SerialName("categoryID")val categoryId:Int,val name:String)
@Serializable data class ALine(val original:String="",val name:String="",@Serializable(with=AssistantDecimal::class)val quantity:String?=null,val ingredientId:Int?=null,val unitId:Int?=null,val unit:String?=null,val notes:String?=null,val localId:String=UUID.randomUUID().toString(),val quantityText:String?=null)
@Serializable data class ARecipe(val title:String="",val description:String="",val instructions:String="",val servings:Int?=2,val prepMinutes:Int?=null,val cookMinutes:Int?=null,val categoryId:Int?=null,val ingredients:List<ALine> = emptyList(),val source:String?=null,val photo:ARecipePhoto?=null)
@Serializable data class ARecipePhoto(val image:String,val thumbnail:String)
@Serializable data class ARecipeDraft(val recipe:ARecipe=ARecipe(),val recipeId:Int?=null,val version:String?=null,val operationId:String=UUID.randomUUID().toString(),val publishedId:Int?=null,val photoKey:String?=null,val localId:String=UUID.randomUUID().toString(),val canEdit:Boolean=true,val sourceKind:String="reviewed",val originJobId:String?=null,val resultIndex:Int?=null)
@Serializable data class ASuggestion(val recipeId:Int,val reason:String)
@Serializable data class AShortage(val name:String,@Serializable(with=AssistantDecimal::class)val required:String,@Serializable(with=AssistantDecimal::class)val available:String,@Serializable(with=AssistantDecimal::class)val missing:String,val needsReview:Boolean)
@Serializable data class AReadiness(val status:Int,val ingredients:List<AShortage>)
@Serializable data class ACandidate(val recipeId:Int,val title:String,val instructions:String,val servings:Int?,val prepMinutes:Int?,val cookMinutes:Int?,val categoryId:Int?,val version:String,val readiness:AReadiness,val nutrition:JsonObject?=null,val ingredients:List<AFullLine> = emptyList())
@OptIn(ExperimentalSerializationApi::class)
@Serializable data class AMeal(val date:String,val slotId:Int,val recipeId:Int?=null,val draftIndex:Int?=null,@Serializable(with=AssistantDecimal::class)val portions:String="2",val batchKey:String=UUID.randomUUID().toString(),val reason:String="",val preparedBatchId:Int?=null,@EncodeDefault(EncodeDefault.Mode.NEVER)val portionsText:String?=null)
@OptIn(ExperimentalSerializationApi::class)
@Serializable data class APurchase(val original:String,val name:String,val ingredientId:Int?=null,@Serializable(with=AssistantDecimal::class)val quantity:String?=null,val unitId:Int?=null,val unit:String?=null,val purchasedOn:String?=null,val expiresOn:String?=null,@EncodeDefault(EncodeDefault.Mode.NEVER)val quantityText:String?=null)
@Serializable data class AProposal(val suggestions:List<ASuggestion> = emptyList(),val recipes:List<ARecipe> = emptyList(),val meals:List<AMeal> = emptyList(),val purchases:List<APurchase> = emptyList(),val warnings:List<String> = emptyList())
@Serializable data class APlanEntry(@SerialName("mealPlanEntryID")val id:Int,val plannedDate:String,@SerialName("categoryID")val slotId:Int,@SerialName("recipeID")val recipeId:Int,val recipeTitle:String,val version:Long,val isPrepared:Boolean=false,@Serializable(with=AssistantDecimal::class)val consumedPortions:String="0",@Serializable(with=AssistantDecimal::class)val allocatedPortions:String="1")
@Serializable data class ABatch(@SerialName("cookingBatchID")val id:Int,val version:Long,val recipeID:Int?=null,@Serializable(with=AssistantDecimal::class)val freePortions:String="0",val expiresOn:String?=null,val cookedAt:String?=null)
@Serializable data class AContext(val databaseId:String,val ingredients:List<AIngredient>,val units:List<AUnit>,val categories:List<ASlot>,val candidates:List<ACandidate>,val plan:List<APlanEntry>,val batches:List<ABatch> = emptyList())
@Serializable data class ACapabilities(val enabled:Boolean,val configured:Boolean,val provider:String,val model:String,val tasks:List<String>,val freeOnly:Boolean,val dataNoticeVersion:String,val retryAfter:String?=null)
@Serializable data class APreferenceResponse(val preferencesJson:String,val noticeVersion:String?=null,val acceptedAt:String?=null)
@Serializable data class AConsent(val noticeVersion:String="gemini-free-1",val accepted:Boolean)
@Serializable data class AJob(val jobId:String,val kind:String,val state:String,val version:Long,val workJson:String?=null,val resultJson:String?=null,val errorCode:String?=null,val createdAt:String="")
@Serializable data class APending(val path:String,val payload:String,val action:String,val recipeIndex:Int?=null,val draftId:String?=null,val ingredientRowId:String?=null)
@Serializable data class AWorkspace(val prompt:String="",val source:String="",val kind:String="suggest",val startDate:String=LocalDate.now().toString(),val preferences:APreferences=APreferences(),val allowNewRecipes:Boolean=false,val uploadId:String?=null,val recipes:List<ARecipeDraft> = emptyList(),val suggestions:List<ASuggestion> = emptyList(),val meals:List<AMeal> = emptyList(),val purchases:List<APurchase> = emptyList(),val selectedPurchases:List<Int> = emptyList(),val acceptedPurchases:List<Int> = emptyList(),val pinned:List<Int> = emptyList(),val replaceIds:List<Int> = emptyList(),val job:AJob?=null,val context:AContext?=null,val proposalContext:AContext?=null,val pending:APending?=null,val planOperation:String=UUID.randomUUID().toString(),val receiptOperation:String=UUID.randomUUID().toString(),val confirmedDuplicate:Boolean=false,val warnings:List<String> = emptyList(),val uploadKind:String?=null,val planJobId:String?=null,val receiptJob:AJob?=null)
@Serializable data class ARecipeInput(val recipeID:Int?,val categoryID:Int?,val title:String,val description:String,val instructions:String,val prepTimeMinutes:Int?,val cookTimeMinutes:Int?,val servings:Int?,val ingredients:List<ARecipeLineInput>,val expectedVersion:String?,val photoMode:String="Keep",val imageData:String?=null,val thumbnailData:String?=null)
@Serializable data class ARecipeLineInput(val ingredientID:Int,val unitID:Int,@Serializable(with=AssistantDecimal::class)val quantity:String,val notes:String?)
@Serializable data class ARecipeSave(val operationId:String,val recipe:ARecipeInput,val source:String?=null,val sourceKind:String="reviewed")
@Serializable data class ARecipeSaved(val id:Int,val version:String)
@Serializable data class APlanApply(val operationId:String,val jobId:String?,val meals:List<AMeal>,val replaceEntryIds:List<Int>,val recipeVersions:Map<Int,String>,val entryVersions:Map<Int,Long>,val batchVersions:Map<Int,Long>)
@Serializable data class AReceiptApply(val operationId:String,val expectedVersion:Long,val rowIndexes:List<Int>,val purchases:List<APurchase>,val snapshotId:Int?=null,val confirmDuplicate:Boolean=false)
@Serializable data class AShopping(val name:String,val unit:String,@Serializable(with=AssistantDecimal::class)val required:String,@Serializable(with=AssistantDecimal::class)val fromPantry:String,@Serializable(with=AssistantDecimal::class)val missing:String,val needsReview:Boolean)
@Serializable data class APreview(val cookingBatches:Int,val cookingMinutes:Int,@Serializable(with=AssistantDecimal::class)val calories:String,@Serializable(with=AssistantDecimal::class)val proteinGrams:String,val nutritionComplete:Boolean,val shopping:List<AShopping>)
@Serializable data class CachedRecipeSearch(val query: String, val rows: List<ABrowseRecipe>)
@Serializable data class ABrowseRecipe(@SerialName("recipeID")val recipeId:Int,val title:String)
@Serializable data class AFullRecipe(@SerialName("recipeID")val recipeId:Int,val title:String,val description:String?=null,val instructions:String,val prepTimeMinutes:Int?,val cookTimeMinutes:Int?,val servings:Int?,val categoryID:Int?,val rowVersion:String,val ingredients:List<AFullLine>,@SerialName("userID")val userId:Int?=null)
@Serializable data class AFullLine(@SerialName("ingredientID")val ingredientId:Int,@SerialName("unitID")val unitId:Int,val ingredientName:String,val unitAbbreviation:String,@Serializable(with=AssistantDecimal::class)val quantity:String,val notes:String?=null)
fun AFullRecipe.draft()=ARecipeDraft(ARecipe(title,description?:"",instructions,servings,prepTimeMinutes,cookTimeMinutes,categoryID,ingredients.map{ALine(it.ingredientName,it.ingredientName,it.quantity,it.ingredientId,it.unitId,it.unitAbbreviation,it.notes)}),recipeId,rowVersion)

fun ACandidate.draft()=ARecipeDraft(ARecipe(title,"",instructions,servings,prepMinutes,cookMinutes,categoryId,ingredients.map{ALine(it.ingredientName,it.ingredientName,it.quantity,it.ingredientId,it.unitId,it.unitAbbreviation,it.notes)}),recipeId,version)
