INSERT INTO Store (Name, Address) VALUES ('Головний Магазин', 'Київ, Хрещатик 1');
INSERT INTO Customer (FirstName, LastName, Email, Phone) VALUES ('Іван', 'Франко', 'ivan@example.com', '0501234567');
INSERT INTO Category (Name, Description) VALUES ('Електроніка', 'Телефони та ноутбуки');
INSERT INTO Supplier (Name, ContactEmail) VALUES ('TechPostach', 'sales@tech.com');

INSERT INTO Product (CategoryId, SupplierId, Name, SKU, Price) 
VALUES (1, 1, 'Ноутбук Apple MacBook Air', 'MAC-AIR-M2', 45000.00);

INSERT INTO Inventory (ProductId, StoreId, Quantity) VALUES (1, 1, 5);