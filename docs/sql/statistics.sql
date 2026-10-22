-- Depot statistics in plain SQL (PostgreSQL), same rules as the statistics API.
-- Run in pgAdmin (Aspire dashboard -> pgadmin -> database TechSpherex-db -> Query Tool).
-- All dates are Vietnam dates: timestamps are converted with AT TIME ZONE 'Asia/Ho_Chi_Minh'
-- before taking the date, so 00:00-07:00 Vietnam time is not counted as the previous day.


-- 1. Containers in the yard now, per line operator and dwell time
--    Dwell days = Vietnam calendar days since gate-in (0 on the gate-in day).
--    Short stay: 0-9 days; long stay: 10 days or more.
--    LEFT JOIN keeps line operators that have no container in the yard (all zeros).
WITH in_yard AS (
    SELECT "LineOperatorId",
           (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
             - ("GateInAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS dwell_days
    FROM "ContainerVisits"
    WHERE "GateOutAt" IS NULL                                       -- still in the yard
)
SELECT
    lo."Code"                                        AS line_operator,
    lo."Name"                                        AS name,
    COUNT(y.dwell_days) FILTER (WHERE y.dwell_days < 10)  AS short_stay_0_9_days,
    COUNT(y.dwell_days) FILTER (WHERE y.dwell_days >= 10) AS long_stay_10_days_plus,
    COUNT(y.dwell_days)                              AS total
FROM "LineOperators" lo
LEFT JOIN in_yard y ON y."LineOperatorId" = lo."Id"
GROUP BY lo."Code", lo."Name"
ORDER BY lo."Code";


-- 2. Gate-in and gate-out volume per line operator per day (Vietnam dates)
--    Change the two dates to select the period (both included).
--    A gate-out counts for the line operator of its visit.
WITH gate_events AS (
    SELECT "LineOperatorId",
           ("GateInAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date  AS day,
           1 AS gate_in,
           0 AS gate_out
    FROM "ContainerVisits"
    UNION ALL
    SELECT "LineOperatorId",
           ("GateOutAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS day,
           0,
           1
    FROM "ContainerVisits"
    WHERE "GateOutAt" IS NOT NULL
)
SELECT
    e.day,
    lo."Code"           AS line_operator,
    SUM(e.gate_in)      AS gate_in,
    SUM(e.gate_out)     AS gate_out
FROM gate_events e
JOIN "LineOperators" lo ON lo."Id" = e."LineOperatorId"
WHERE e.day BETWEEN DATE '2026-10-01' AND DATE '2026-10-31'
GROUP BY e.day, lo."Code"
ORDER BY e.day, lo."Code";


-- 3. Containers in the yard with their dwell days, longest first, ranked per line operator
--    (window function: RANK() restarts for each line operator)
SELECT
    lo."Code"                                                        AS line_operator,
    c."ContainerNumber"                                              AS container_number,
    c."Size"                                                         AS size,
    b."Code"                                                         AS block,
    v."Bay", v."Row", v."Tier",
    (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
      - (v."GateInAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date        AS dwell_days,
    RANK() OVER (PARTITION BY lo."Code" ORDER BY v."GateInAt")       AS rank_in_line_operator
FROM "ContainerVisits" v
JOIN "Containers"    c  ON c."Id"  = v."ContainerId"
JOIN "LineOperators" lo ON lo."Id" = v."LineOperatorId"
JOIN "Blocks"        b  ON b."Id"  = v."BlockId"
WHERE v."GateOutAt" IS NULL
ORDER BY lo."Code", rank_in_line_operator;
