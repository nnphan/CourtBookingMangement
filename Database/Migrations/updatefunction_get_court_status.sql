-- DROP FUNCTION booking.fn_get_court_status(uuid, date);

CREATE OR REPLACE FUNCTION booking.fn_get_court_status(p_branch_id uuid, p_date date)
 RETURNS TABLE(branch_id uuid, branch_name character varying, open_time time without time zone, close_time time without time zone, court_id uuid, court_number integer, court_name character varying, court_status character varying, booking_id uuid, start_time time without time zone, end_time time without time zone, booking_date date, booking_type character varying, booking_status character varying, payment_status character varying, customer_name character varying, phone_number character varying, title character varying, color character varying)
 LANGUAGE sql
AS $function$

SELECT

    b.id,
    b.name,
    oh.open_time,
    oh.close_time,
    c.id,
    c.court_number,
    COALESCE(c.name,'') AS court_name,
    c.status,
    bk.id,
    bd.start_time,
    bd.end_time,
    bd.booking_date,
    bk.booking_type,
    bk.status,
    COALESCE(pay.status,'UNPAID'),
    cust.full_name,
    cust.phone_number,
    CONCAT(
        cust.full_name,
        ' - ',
        cust.phone_number
    ),

    CASE
        WHEN bk.booking_type = 'FIXED'
            THEN '#35A8DB'

        WHEN bk.booking_type = 'FLEXIBLE'
            THEN '#E2B93B'

        ELSE '#CCCCCC'
    END

FROM core.branches b INNER JOIN core.operating_hours oh ON oh.branch_id = b.id
INNER JOIN core.courts c ON c.branch_id = b.id
LEFT JOIN booking.booking_details bd on bd.court_id = c.id AND bd.booking_date = p_date
LEFT JOIN booking.bookings bk ON bk.id  = bd.booking_id  AND  bk.deleted_at IS NULL AND bk.status <> 'expired'
LEFT JOIN customer.customers cust ON cust.id = bk.customer_id
LEFT JOIN
(
    SELECT
        booking_id,
        status
    FROM payment.payments
    GROUP BY booking_id, status
) pay ON pay.booking_id = bk.id

WHERE
    b.id = p_branch_id
    AND c.deleted_at IS NULL

ORDER BY
    c.court_number,
    bd.start_time;

$function$
;