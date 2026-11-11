package app.mealprep.companion

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Entity(tableName="shopping_items",indices=[Index(value=["serverId"],unique=true)])
data class ItemEntity(@PrimaryKey val localId:String,val serverId:Int?,val snapshotId:Int,val version:Long,val payload:String,val hasConflict:Boolean=false,val conflictPayload:String?=null)
@Entity(tableName="outbox",indices=[Index(value=["operationId"],unique=true),Index("localId")])
data class OutboxEntity(@PrimaryKey(autoGenerate=true)val sequence:Long=0,val operationId:String,val localId:String,val kind:String,val payload:String,val state:String="queued")
@Entity(tableName="cache",primaryKeys=["kind","id"])
data class CacheEntity(val kind:String,val id:String,val payload:String)
@Dao interface CompanionDao {
 @Query("SELECT * FROM shopping_items ORDER BY localId")fun observeItems():Flow<List<ItemEntity>>
 @Query("SELECT * FROM outbox ORDER BY sequence")fun observeOutbox():Flow<List<OutboxEntity>>
 @Query("SELECT * FROM cache ORDER BY kind,id")fun observeCache():Flow<List<CacheEntity>>
 @Query("SELECT * FROM shopping_items")suspend fun items():List<ItemEntity>
 @Query("SELECT * FROM shopping_items WHERE localId=:id")suspend fun item(id:String):ItemEntity?
 @Query("SELECT * FROM shopping_items WHERE serverId=:id")suspend fun serverItem(id:Int):ItemEntity?
 @Insert(onConflict=OnConflictStrategy.REPLACE)suspend fun putItem(row:ItemEntity)
 @Query("DELETE FROM shopping_items WHERE localId=:id")suspend fun removeItem(id:String)
 @Insert suspend fun enqueue(row:OutboxEntity):Long
 @Query("SELECT * FROM outbox ORDER BY sequence")suspend fun outbox():List<OutboxEntity>
 @Query("SELECT * FROM outbox WHERE state!='conflict' ORDER BY sequence")suspend fun pending():List<OutboxEntity>
 @Query("UPDATE outbox SET state=:state WHERE sequence=:sequence")suspend fun setState(sequence:Long,state:String)
 @Query("UPDATE outbox SET state='conflict' WHERE localId=:localId")suspend fun block(localId:String)
 @Query("DELETE FROM outbox WHERE sequence=:sequence")suspend fun acknowledge(sequence:Long)
 @Query("DELETE FROM outbox WHERE localId=:localId")suspend fun clearOperations(localId:String)
 @Insert(onConflict=OnConflictStrategy.REPLACE)suspend fun putCache(rows:List<CacheEntity>)
 @Query("DELETE FROM cache WHERE kind!='review'")suspend fun clearCache()
 @Query("DELETE FROM cache")suspend fun clearAllCache()
 @Query("DELETE FROM cache WHERE kind='review' AND id=:subject")suspend fun clearReview(subject:String)
 @Query("DELETE FROM shopping_items")suspend fun clearItems()
 @Query("DELETE FROM outbox")suspend fun clearOutbox()
 @Query("SELECT * FROM cache WHERE kind=:kind")suspend fun cache(kind:String):List<CacheEntity>
}
@Database(entities=[ItemEntity::class,OutboxEntity::class,CacheEntity::class],version=1,exportSchema=true)
abstract class ShoppingDatabase:RoomDatabase(){abstract fun dao():CompanionDao}
