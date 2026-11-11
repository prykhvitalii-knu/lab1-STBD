using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Bogus;
using Dapper;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Npgsql;
using StackExchange.Redis;

string pgConnectionString = "Host=localhost;Port=5432;Database=StoreDB;Username=store_admin;Password=SecretPassword123!";
string mongoConnectionString = "mongodb://mongo_admin:MongoPassword123!@localhost:27017";
string redisConnectionString = "localhost:6379";

int productsCount = 1000; 
int benchmarkReads = 500; 

using var pgConnection = new NpgsqlConnection(pgConnectionString);
pgConnection.Open();

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDb = mongoClient.GetDatabase("StoreDB");
var mongoCollection = mongoDb.GetCollection<MongoProduct>("Products");

var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
var redisDb = redisConnection.GetDatabase();

Console.WriteLine("======================================================");
Console.WriteLine("ЛАБОРАТОРНА РОБОТА 2: SQL vs NoSQL (Mongo & Redis)");
Console.WriteLine("======================================================\n");

Console.WriteLine($"[1] Генеруємо {productsCount} товарів за допомогою Bogus...");

pgConnection.Execute("TRUNCATE TABLE Category, Supplier, Product, ProductReview, ProductAttribute, ProductAttributeValue CASCADE;");
mongoDb.DropCollection("Products");

int catId = pgConnection.QuerySingle<int>("INSERT INTO Category (Name) VALUES ('Електроніка') RETURNING Id;");
int supId = pgConnection.QuerySingle<int>("INSERT INTO Supplier (Name) VALUES ('Global Tech') RETURNING Id;");
int attrColorId = pgConnection.QuerySingle<int>("INSERT INTO ProductAttribute (Name) VALUES ('Колір') RETURNING Id;");
int attrWeightId = pgConnection.QuerySingle<int>("INSERT INTO ProductAttribute (Name) VALUES ('Вага') RETURNING Id;");

var reviewFaker = new Faker<MongoReview>()
    .RuleFor(r => r.AuthorName, f => f.Name.FullName())
    .RuleFor(r => r.Rating, f => f.Random.Int(1, 5))
    .RuleFor(r => r.ReviewText, f => f.Lorem.Sentence());

var productFaker = new Faker<MongoProduct>()
    .RuleFor(p => p._id, f => f.IndexFaker + 1)
    .RuleFor(p => p.Name, f => f.Commerce.ProductName())
    .RuleFor(p => p.SKU, f => f.Commerce.Ean13())
    .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(100, 5000), 2))
    .RuleFor(p => p.Reviews, f => reviewFaker.Generate(3)) 
    .RuleFor(p => p.Attributes, f => new List<MongoAttribute>
    {
        new MongoAttribute { Key = "Колір", Value = f.Commerce.Color() },
        new MongoAttribute { Key = "Вага", Value = f.Random.Int(100, 2000) + " г" }
    });

var fakeProducts = productFaker.Generate(productsCount);

Console.WriteLine("[2] Записуємо дані в PostgreSQL (нормалізовано по 4 таблицях)...");
using (var tx = pgConnection.BeginTransaction())
{
    foreach (var p in fakeProducts)
    {
        string sqlProd = "INSERT INTO Product (CategoryId, SupplierId, Name, SKU, Price) VALUES (@CId, @SId, @Name, @SKU, @Price) RETURNING Id;";
        int newId = pgConnection.QuerySingle<int>(sqlProd, new { CId = catId, SId = supId, p.Name, p.SKU, p.Price }, tx);
        
        foreach (var r in p.Reviews)
        {
            pgConnection.Execute("INSERT INTO ProductReview (ProductId, AuthorName, Rating, ReviewText) VALUES (@PId, @A, @R, @T);", 
                new { PId = newId, A = r.AuthorName, R = r.Rating, T = r.ReviewText }, tx);
        }

        pgConnection.Execute("INSERT INTO ProductAttributeValue (ProductId, AttributeId, Value) VALUES (@PId, @AId, @V);", 
            new { PId = newId, AId = attrColorId, V = p.Attributes[0].Value }, tx);
        pgConnection.Execute("INSERT INTO ProductAttributeValue (ProductId, AttributeId, Value) VALUES (@PId, @AId, @V);", 
            new { PId = newId, AId = attrWeightId, V = p.Attributes[1].Value }, tx);
    }
    tx.Commit();
}

Console.WriteLine("[3] Записуємо дані в MongoDB (як цілісні JSON-документи)...");
mongoCollection.InsertMany(fakeProducts);
Console.WriteLine("Дані успішно згенеровано!\n");


Console.WriteLine($"[4] BENCHMARK: Читання {benchmarkReads} випадкових сторінок товарів (Товар + Відгуки + Характеристики)");
var stopwatch = new Stopwatch();
var random = new Random();

// TEST PostgreSQL
stopwatch.Start();
for (int i = 0; i < benchmarkReads; i++)
{
    int randomId = random.Next(1, productsCount);
    var product = pgConnection.QueryFirstOrDefault("SELECT * FROM Product WHERE Id = @Id", new { Id = randomId });
    var reviews = pgConnection.Query("SELECT * FROM ProductReview WHERE ProductId = @Id", new { Id = randomId }).ToList();
    var attributes = pgConnection.Query(@"
        SELECT pa.Name, pav.Value FROM ProductAttributeValue pav 
        JOIN ProductAttribute pa ON pav.AttributeId = pa.Id WHERE pav.ProductId = @Id", new { Id = randomId }).ToList();
}
stopwatch.Stop();
long sqlTime = stopwatch.ElapsedMilliseconds;
Console.WriteLine($" > PostgreSQL (3 SQL запити + JOIN) час: {sqlTime} мс");

// TEST MongoDB 
stopwatch.Reset();
stopwatch.Start();
for (int i = 0; i < benchmarkReads; i++)
{
    int randomId = random.Next(1, productsCount);
    var filter = Builders<MongoProduct>.Filter.Eq(p => p._id, randomId);
    var productDocument = mongoCollection.Find(filter).FirstOrDefault();
}
stopwatch.Stop();
long mongoTime = stopwatch.ElapsedMilliseconds;
Console.WriteLine($" > MongoDB (1 запит цілісного документа) час: {mongoTime} мс");
if (mongoTime < sqlTime)
{
    Console.WriteLine($" > MongoDB швидша на {sqlTime - mongoTime} мс");
    Console.WriteLine("Висновок: Для ієрархічних даних зі змінною структурою Document-oriented БД ефективніша.\n");
}
else
{
    Console.WriteLine($" > PostgreSQL швидша на {mongoTime - sqlTime} мс");
    Console.WriteLine("Висновок: Для ієрархічних даних зі змінною структурою Document-oriented БД не ефективніша.\n");
}




// REDIS TEST 
Console.WriteLine("ТЕСТ REDIS: Збереження кошика покупця");
Console.WriteLine("пояснення: Кошик має жити недовго, та бути швидко-доступним..");

string cartKey = "user_cart:999";
var cartItems = new List<object> 
{ 
    new { ProductId = 1, Quantity = 2, Price = 45000 },
    new { ProductId = 42, Quantity = 1, Price = 300 }
};

string jsonCart = JsonSerializer.Serialize(cartItems);
redisDb.StringSet(cartKey, jsonCart, TimeSpan.FromMinutes(30)); 
Console.WriteLine($" > Дані кошика збережено в Redis за ключем '{cartKey}'.");


string retrievedCart = redisDb.StringGet(cartKey);
Console.WriteLine($" > Прочитано з Redis: {retrievedCart}");

Console.WriteLine("\n=========================================");
Console.WriteLine("ПЕРЕВІРКУ ЗАВЕРШЕНО.");

public class MongoProduct
{
    [BsonId]
    public int _id { get; set; }
    public string Name { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public List<MongoAttribute> Attributes { get; set; } = new();
    public List<MongoReview> Reviews { get; set; } = new();
}

public class MongoAttribute
{
    public string Key { get; set; }
    public string Value { get; set; }
}

public class MongoReview
{
    public string AuthorName { get; set; }
    public int Rating { get; set; }
    public string ReviewText { get; set; }
}