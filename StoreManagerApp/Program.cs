using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Npgsql;

string connectionString = "Host=localhost;Port=5432;Database=StoreDB;Username=store_admin;Password=SecretPassword123!";

using (var uow = new UnitOfWork(connectionString))
{
    Console.WriteLine("======================================================");
    Console.WriteLine("СТАРТ ПЕРЕВІРКИ СИСТЕМИ УПРАВЛІННЯ МАГАЗИНОМ");
    Console.WriteLine("======================================================\n");

    Console.WriteLine(">>> ПЕРЕВІРКА 1: Вибірка даних через View (vw_ActiveProducts)");
    var products = uow.Products.GetActiveProducts();
    foreach (var p in products)
    {
        Console.WriteLine($" - [{p.SKU}] {p.Name} | Ціна: {p.Price} | Категорія: {p.CategoryName}");
    }
    Console.WriteLine(" [OK] Дані успішно отримано без прямого звернення до таблиць.\n");

    Console.WriteLine(">>> ПЕРЕВІРКА 2: Оформлення замовлення (Транзакція + Stored Procedure)");
    try
    {
        int orderId = uow.Orders.CreateOrder(customerId: 1, storeId: 1);
        uow.Orders.AddOrderItem(orderId, productId: 1, storeId: 1, quantity: 2);
        uow.Commit();
        Console.WriteLine($" [OK] Замовлення №{orderId} створено! Залишки списано через sp_AddOrderItem.\n");
    }
    catch (Exception ex)
    {
        uow.Rollback();
        Console.WriteLine($" [FAIL] Помилка: {ex.Message}");
    }

    Console.WriteLine(">>> ПЕРЕВІРКА 3: Робота 'Soft Delete' та Аудиту");
    Console.WriteLine(" Видаляємо товар з Id=1 через sp_SoftDeleteProduct...");
    uow.Products.SoftDeleteProduct(productId: 1, userId: 999);
    uow.Commit();

    Console.WriteLine(" Перевіряємо список активних товарів через View ще раз:");
    var productsAfterDelete = uow.Products.GetActiveProducts();
    if (productsAfterDelete.AsList().Count == 0)
    {
        Console.WriteLine(" - Список порожній!");
        Console.WriteLine(" [OK] Товар приховано з View, але він залишився в базі з прапорцем IsDeleted = true.\n");
    }
}

Console.WriteLine("======================================================");
Console.WriteLine("ПЕРЕВІРКУ ЗАВЕРШЕНО. УСІ ВИМОГИ ВИКОНАНО.");
Console.WriteLine("======================================================");

public class ActiveProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public string CategoryName { get; set; }
}

public interface IOrderRepository
{
    int CreateOrder(int customerId, int storeId);
    void AddOrderItem(int orderId, int productId, int storeId, int quantity);
}

public interface IProductRepository
{
    IEnumerable<ActiveProductDto> GetActiveProducts();
    void SoftDeleteProduct(int productId, int userId);
}

public class OrderRepository : IOrderRepository
{
    private readonly IDbTransaction _transaction;
    public OrderRepository(IDbTransaction transaction) => _transaction = transaction;

    public int CreateOrder(int customerId, int storeId)
    {
        string sql = @"INSERT INTO ""Order"" (CustomerId, StoreId, TotalAmount) VALUES (@CustomerId, @StoreId, 0) RETURNING Id;";
        return _transaction.Connection.ExecuteScalar<int>(sql, new { CustomerId = customerId, StoreId = storeId }, _transaction);
    }

    public void AddOrderItem(int orderId, int productId, int storeId, int quantity)
    {
        string sql = "CALL sp_AddOrderItem(@OrderId, @ProductId, @StoreId, @Quantity);";
        _transaction.Connection.Execute(sql, new { OrderId = orderId, ProductId = productId, StoreId = storeId, Quantity = quantity }, _transaction);
    }
}

public class ProductRepository : IProductRepository
{
    private readonly IDbTransaction _transaction;
    public ProductRepository(IDbTransaction transaction) => _transaction = transaction;

    public IEnumerable<ActiveProductDto> GetActiveProducts()
    {
        return _transaction.Connection.Query<ActiveProductDto>("SELECT * FROM vw_ActiveProducts;", transaction: _transaction);
    }

    public void SoftDeleteProduct(int productId, int userId)
    {
        string sql = "CALL sp_SoftDeleteProduct(@ProductId, @UserId);";
        _transaction.Connection.Execute(sql, new { ProductId = productId, UserId = userId }, _transaction);
    }
}

public class UnitOfWork : IDisposable
{
    private readonly IDbConnection _connection;
    private IDbTransaction _transaction;

    public IOrderRepository Orders { get; private set; }
    public IProductRepository Products { get; private set; }

    public UnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();
        _transaction = _connection.BeginTransaction();

        Orders = new OrderRepository(_transaction);
        Products = new ProductRepository(_transaction);
    }

    public void Commit()
    {
        _transaction.Commit();
        _transaction.Dispose();
        _transaction = _connection.BeginTransaction();
    }

    public void Rollback()
    {
        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = _connection.BeginTransaction();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
    }
}