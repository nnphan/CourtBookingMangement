ALTER TABLE booking.bookings
ALTER COLUMN booking_type TYPE varchar(50);

ALTER TABLE booking.bookings
ALTER COLUMN payment_method TYPE varchar(50);

ALTER TABLE booking.bookings
ALTER COLUMN status TYPE varchar(50);

ALTER TABLE booking.bookings
ALTER COLUMN booking_code TYPE varchar(50);


ALTER TABLE booking.bookings
RENAME COLUMN discount_amount
TO paid_amount;