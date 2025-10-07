-- orders_database.sql
-- Создание базы данных для заказов ТМК

-- Таблица заказов
CREATE TABLE IF NOT EXISTS orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_number TEXT UNIQUE NOT NULL,
    customer_name TEXT NOT NULL,
    customer_phone TEXT NOT NULL,
    customer_email TEXT NOT NULL,
    customer_company TEXT,
    delivery_address TEXT,
    order_comment TEXT,
    order_status TEXT DEFAULT 'new' CHECK(order_status IN ('new', 'processing', 'confirmed', 'shipped', 'delivered', 'cancelled')),
    total_amount REAL NOT NULL,
    discount_amount REAL DEFAULT 0,
    subtotal_amount REAL NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Таблица товаров в заказах
CREATE TABLE IF NOT EXISTS order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL,
    product_type TEXT NOT NULL,
    diameter REAL,
    wall_thickness REAL,
    gost TEXT,
    steel_grade TEXT,
    warehouse TEXT,
    quantity REAL NOT NULL,
    unit TEXT NOT NULL CHECK(unit IN ('meters', 'tons')),
    unit_price REAL NOT NULL,
    total_price REAL NOT NULL,
    discount_percentage REAL DEFAULT 0,
    FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
);

-- Таблица для хранения информации о ценах и скидках
CREATE TABLE IF NOT EXISTS price_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id TEXT NOT NULL,
    price_t REAL NOT NULL,
    price_limit_t1 REAL,
    price_t1 REAL,
    price_limit_t2 REAL,
    price_t2 REAL,
    price_m REAL NOT NULL,
    price_limit_m1 REAL,
    price_m1 REAL,
    price_limit_m2 REAL,
    price_m2 REAL,
    nds INTEGER DEFAULT 20,
    valid_from DATETIME DEFAULT CURRENT_TIMESTAMP,
    valid_to DATETIME,
    is_active BOOLEAN DEFAULT 1
);

-- Таблица для логирования изменений статусов заказов
CREATE TABLE IF NOT EXISTS order_status_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL,
    old_status TEXT,
    new_status TEXT NOT NULL,
    changed_by TEXT DEFAULT 'system',
    change_reason TEXT,
    changed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
);

-- Индексы для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(order_status);
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON orders(created_at);
CREATE INDEX IF NOT EXISTS idx_orders_customer_email ON orders(customer_email);
CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON order_items(product_id);
CREATE INDEX IF NOT EXISTS idx_price_history_product_id ON price_history(product_id);
CREATE INDEX IF NOT EXISTS idx_price_history_active ON price_history(is_active);

-- Триггер для автоматического обновления updated_at
CREATE TRIGGER IF NOT EXISTS update_orders_timestamp 
AFTER UPDATE ON orders
BEGIN
    UPDATE orders SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
END;

-- Триггер для логирования изменений статуса заказа
CREATE TRIGGER IF NOT EXISTS log_order_status_change 
AFTER UPDATE OF order_status ON orders
BEGIN
    INSERT INTO order_status_log (order_id, old_status, new_status, change_reason)
    VALUES (OLD.id, OLD.order_status, NEW.order_status, 'Status changed via system');
END;