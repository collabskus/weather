Please review the code again to make sure it does not make ANY unnecessary queries to the NWS. Looks like we have multiple queries to the alert endpoint 

```json
{
  "id": "456ee568fca4149b",
  "parentId": "93998f301811cc05",
  "traceId": "b54726f157933fb24b9802968d6ed930",
  "projectId": 8736,
  "groupId": "3466442550821956506",
  "type": "httpclient",
  "system": "httpclient:weather-api",
  "kind": "client",
  "name": "GET",
  "displayName": "GET https://api.weather.gov/alerts/active",
  "time": "2026-06-23T13:21:05.956Z",
  "duration": 49.145,
  "statusCode": "ok",
  "attrs": {
    "telemetry_sdk_language::str": "dotnet",
    "telemetry_sdk_version::str": "1.16.0",
    "http_response_status_class::str": "2xx",
    "otel_library_name::str": "System.Net.Http",
    "telemetry_sdk_name::str": "opentelemetry",
    "url_full::str": "https://api.weather.gov/alerts/active?*",
    "http_request_method::str": "GET",
    "http_response_status_code::int": 200,
    "network_protocol_version::str": "1.1",
    "server_address::str": "api.weather.gov",
    "server_port::int": 443,
    "service_name::str": "weather-api"
  },
  "events": [],
  "links": []
}
```

please fix everything properly and give me the full file for all files that need to change
