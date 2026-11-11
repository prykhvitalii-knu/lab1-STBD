CREATE TABLE ProductReview (
    Id SERIAL PRIMARY KEY,
    ProductId INT NOT NULL,
    AuthorName VARCHAR(100) NOT NULL,
    Rating INT CHECK (Rating >= 1 AND Rating <= 5),
    ReviewText TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ProductId) REFERENCES Product(Id) ON DELETE CASCADE
);

CREATE TABLE ProductAttribute (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) UNIQUE NOT NULL
);

CREATE TABLE ProductAttributeValue (
    ProductId INT NOT NULL,
    AttributeId INT NOT NULL,
    Value VARCHAR(255) NOT NULL,
    PRIMARY KEY (ProductId, AttributeId),
    FOREIGN KEY (ProductId) REFERENCES Product(Id) ON DELETE CASCADE,
    FOREIGN KEY (AttributeId) REFERENCES ProductAttribute(Id) ON DELETE CASCADE
);

CREATE INDEX idx_review_product ON ProductReview(ProductId);
CREATE INDEX idx_attrvalue_product ON ProductAttributeValue(ProductId);