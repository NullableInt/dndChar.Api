﻿using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace dndChar.Database
{
    public class DocumentStoreHolder
    {
        public DocumentStoreHolder()
        {
            var url = System.Environment.GetEnvironmentVariable("MongoUrl");
            Store = new MongoClient(url);
        }


        public IMongoClient Store { get; private set; }
    }
}
