CREATE INDEX idx_employee_department ON Employee(DepartmentId);
CREATE INDEX idx_product_category ON Product(CategoryId);
CREATE INDEX idx_order_customer ON "Order"(CustomerId);
CREATE INDEX idx_orderitem_order ON OrderItem(OrderId);

CREATE INDEX idx_product_name ON Product(Name);
CREATE INDEX idx_customer_email ON Customer(Email);

CREATE INDEX idx_active_products ON Product(Id) WHERE IsDeleted = FALSE;
CREATE INDEX idx_active_employees ON Employee(Id) WHERE IsDeleted = FALSE;
CREATE INDEX idx_active_customers ON Customer(Id) WHERE IsDeleted = FALSE;
CREATE INDEX idx_active_suppliers ON Supplier(Id) WHERE IsDeleted = FALSE;