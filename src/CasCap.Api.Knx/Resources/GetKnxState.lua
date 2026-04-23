local hsKeyValue        =   @hsKeyValue
local hsKeyTimestampUtc =   @hsKeyTimestampUtc
local hsKeyValueLabel   =   @hsKeyValueLabel

local keyGroupAddress   =   @keyGroupAddress

-- Observability: increment a per-day call counter in a Redis hash (7-day TTL).
-- Aligned with the CasCap.Common.Caching StringGetSetExpiryAsync.lua pattern.
local trackKey          =   @trackKey
local trackCaller       =   @trackCaller

redis.call('HINCRBY', trackKey, trackCaller, 1)
redis.call('EXPIRE', trackKey, 604800)

local _value        = redis.call('HGET', hsKeyValue, keyGroupAddress)
local _timestampUtc = redis.call('HGET', hsKeyTimestampUtc, keyGroupAddress)
local _valueLabel   = redis.call('HGET', hsKeyValueLabel, keyGroupAddress)

return {_value, _timestampUtc, _valueLabel}
