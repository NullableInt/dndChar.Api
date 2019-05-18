﻿using dndChar.Database;
using dndCharApi.Models;
using dndCharApi.Models.CallOfCthulu;
using dndCharApi.Models.RpgChar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace dndCharApi.Controllers
{
    [Route("")]
    [Authorize]
    public class GenericCharacterController : Controller
    {
        private readonly IEnumerable<ICharacterSheet> characterSheetTypes;

        public IMongoDatabase MongoDb { get; set; }

        public GenericCharacterController(DocumentStoreHolder holder, IEnumerable<ICharacterSheet> characterSheetTypes)
        {
            MongoDb = holder.Store.GetDatabase("RpgCharModelDb");
            this.characterSheetTypes = characterSheetTypes;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAllById([FromRoute] string id)
        {
            var collection = MongoDb.GetCollection<dynamic>("RpgCharModels");
            var stringId = id.ToString();
            var filter = Builders<dynamic>.Filter.Eq("_id", ObjectId.Parse(stringId));
            var list = await (await collection.FindAsync(filter)).ToListAsync();

            if (list.Count > 0)
            {
                return Ok(list[0]);
            }
            return NotFound();
        }

        [HttpGet("{id}/{property}")]
        public async Task<IActionResult> GetBySomePropertyIfItHasIt([FromRoute] string id, [FromRoute] string property)
        {
            var collection = MongoDb.GetCollection<dynamic>("RpgCharModels");
            var stringId = id.ToString();
            var filter = Builders<dynamic>.Filter.Eq("_id", ObjectId.Parse(stringId));
            var list = await (await collection.FindAsync(filter)).ToListAsync();

            if (list.Count > 0)
            {
                var theOne = list[0] as BaseCharacterSheet;
                var props = theOne.GetType().GetProperties();
                var thePropertyIfFound = props.FirstOrDefault(e => e.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
                if (thePropertyIfFound != null)
                {
                    return Json(thePropertyIfFound.GetValue(theOne));
                }
            }
            return BadRequest();
        }

        [HttpPatch("{id}/{property}")]
        public async Task<IActionResult> SetBySomePropertyIfItHasIt([FromRoute] string id,
                                                                    [FromRoute] string property,
                                                                    [FromBody] string propertyValue)
        {
            var collection = MongoDb.GetCollection<dynamic>("RpgCharModels");
            var stringId = id.ToString();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var filter = Builders<dynamic>.Filter.And(
                            Builders<dynamic>.Filter.Eq("_id", ObjectId.Parse(stringId)),
                            Builders<dynamic>.Filter.Eq("ownerID", userId));
            var list = await (await collection.FindAsync(filter)).ToListAsync();

            if (list.Count > 0)
            {
                var theOne = list[0] as BaseCharacterSheet;
                var props = theOne.GetType().GetProperties();
                var thePropertyIfFound = props.FirstOrDefault(e => e.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
                if (thePropertyIfFound != null)
                {
                    try
                    {
                        var value = Convert.ChangeType(propertyValue, thePropertyIfFound.PropertyType);
                        thePropertyIfFound.SetValue(theOne, value);

                        await collection.UpdateOneAsync(filter, Builders<dynamic>.Update.Set(property, value).CurrentDate("_lastUpdated"));
                        return Ok(propertyValue);
                    }
                    catch (Exception)
                    {
                        return BadRequest("fuck");
                    }
                    
                }
            }
            return BadRequest();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteById([FromRoute] string id)
        {
            var collection = MongoDb.GetCollection<dynamic>("RpgCharModels");
            var deleteCollection = MongoDb.GetCollection<dynamic>("RpgCharModelsDeleted");
            var stringId = id.ToString();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var filter = Builders<dynamic>.Filter.And(
                            Builders<dynamic>.Filter.Eq("_id", ObjectId.Parse(stringId)),
                            Builders<dynamic>.Filter.Eq("ownerID", userId));
            var list = await collection.FindAsync(filter);

            await deleteCollection.InsertManyAsync(list.ToEnumerable());
            await deleteCollection.UpdateManyAsync(filter, Builders<dynamic>.Update.CurrentDate("_lastUpdated"));
            await collection.DeleteManyAsync(filter);
            return Ok(id);
        }


        [HttpPost("create/{gameSystem}")]
        public async Task<IActionResult> CreateSomeCharacterIfYouCanBoi([FromRoute] string gameSystem, [FromBody] dynamic body)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var type = characterSheetTypes.FirstOrDefault(s => s.GetType().Name.Equals(gameSystem, StringComparison.OrdinalIgnoreCase));
            if (type == null)
            {
                return BadRequest();
            }   

            var deserializedGenericGoo = Newtonsoft.Json.JsonConvert.DeserializeObject(body.ToString(), type.GetType());
            if (deserializedGenericGoo == null)
            {
                return BadRequest();
            }
            
            var firmlyShapedGoo = deserializedGenericGoo as BaseCharacterSheet;
            firmlyShapedGoo.Id = ObjectId.GenerateNewId().ToString();
            firmlyShapedGoo.OwnerID = userId;
            firmlyShapedGoo._created = new BsonDateTime(System.DateTime.UtcNow);
            firmlyShapedGoo._lastUpdated = new BsonDateTime(System.DateTime.UtcNow);

            await MongoDb.GetCollection<dynamic>("RpgCharModels").InsertOneAsync(deserializedGenericGoo);

            return Ok(firmlyShapedGoo.Id);
        }
    }
}
