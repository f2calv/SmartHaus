local hsKeyValue        =   @hsKeyValue
local hsKeyTimestampUtc =   @hsKeyTimestampUtc
local hsKeyValueLabel   =   @hsKeyValueLabel

local keyGroupAddress   =   @keyGroupAddress

local valueValue        =   @valueValue
local valueTimestampUtc =   @valueTimestampUtc
local valueValueLabel   =   @valueValueLabel

-- Observability: increment a per-day call counter in a Redis hash (7-day TTL).
-- Aligned with the CasCap.Common.Caching StringGetSetExpiryAsync.lua pattern.
local trackKey          =   @trackKey
local trackCaller       =   @trackCaller

redis.call('HINCRBY', trackKey, trackCaller, 1)
redis.call('EXPIRE', trackKey, 604800)

local function isempty(s)
  return s == nil or s == ''
end

redis.call('HSET', hsKeyValue, keyGroupAddress, valueValue)
redis.call('HSET', hsKeyTimestampUtc, keyGroupAddress, valueTimestampUtc)
--if (valueValueLabel ~= nil) then
if not isempty(valueValueLabel) then
    redis.call('HSET', hsKeyValueLabel, keyGroupAddress, valueValueLabel)
end
