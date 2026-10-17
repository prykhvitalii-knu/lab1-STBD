CREATE OR REPLACE FUNCTION fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.UpdatedAt = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_employee_update 
BEFORE UPDATE ON Employee 
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

CREATE TRIGGER trg_product_update 
BEFORE UPDATE ON Product 
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();

CREATE TRIGGER trg_customer_update 
BEFORE UPDATE ON Customer 
FOR EACH ROW EXECUTE FUNCTION fn_update_timestamp();


CREATE OR REPLACE VIEW vw_ActiveProducts AS
SELECT p.Id, p.Name, p.SKU, p.Price, c.Name AS CategoryName, s.Name AS SupplierName
FROM Product p
JOIN Category c ON p.CategoryId = c.Id
JOIN Supplier s ON p.SupplierId = s.Id
WHERE p.IsDeleted = FALSE;

CREATE OR REPLACE VIEW vw_ActiveEmployees AS
SELECT e.Id, e.FirstName, e.LastName, e.Email, d.Name AS DepartmentName, pos.Title AS PositionTitle
FROM Employee e
JOIN Department d ON e.DepartmentId = d.Id
JOIN Position pos ON e.PositionId = pos.Id
WHERE e.IsDeleted = FALSE;

CREATE OR REPLACE VIEW vw_InventoryStatus AS
SELECT i.Id, p.Name AS ProductName, p.SKU, st.Name AS StoreName, i.Quantity, i.UpdatedAt, i.UpdatedBy
FROM Inventory i
JOIN Product p ON i.ProductId = p.Id
JOIN Store st ON i.StoreId = st.Id
WHERE p.IsDeleted = FALSE;


CREATE OR REPLACE PROCEDURE sp_SoftDeleteProduct(p_ProductId INT, p_UserId INT)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE Product 
    SET IsDeleted = TRUE, 
        DeletedAt = CURRENT_TIMESTAMP, 
        UpdatedBy = p_UserId 
    WHERE Id = p_ProductId;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_UpdateEmployee(
    p_EmployeeId INT, 
    p_DepartmentId INT, 
    p_PositionId INT, 
    p_FirstName VARCHAR, 
    p_LastName VARCHAR, 
    p_UserId INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE Employee
    SET DepartmentId = p_DepartmentId, 
        PositionId = p_PositionId, 
        FirstName = p_FirstName, 
        LastName = p_LastName, 
        UpdatedBy = p_UserId
    WHERE Id = p_EmployeeId;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_AddOrderItem(
    p_OrderId INT, 
    p_ProductId INT, 
    p_StoreId INT, 
    p_Quantity INT
)
LANGUAGE plpgsql
AS $$
DECLARE 
    v_Price DECIMAL(12,2);
    v_Stock INT;
BEGIN
    SELECT Quantity INTO v_Stock 
    FROM Inventory 
    WHERE ProductId = p_ProductId AND StoreId = p_StoreId;
    
    IF v_Stock IS NULL OR v_Stock < p_Quantity THEN
        RAISE EXCEPTION 'Not enough stock for product % at store %', p_ProductId, p_StoreId;
    END IF;

    SELECT Price INTO v_Price FROM Product WHERE Id = p_ProductId;

    INSERT INTO OrderItem (OrderId, ProductId, Quantity, UnitPrice)
    VALUES (p_OrderId, p_ProductId, p_Quantity, v_Price);

    UPDATE Inventory 
    SET Quantity = Quantity - p_Quantity
    WHERE ProductId = p_ProductId AND StoreId = p_StoreId;
    
    UPDATE "Order"
    SET TotalAmount = TotalAmount + (v_Price * p_Quantity)
    WHERE Id = p_OrderId;
END;
$$;