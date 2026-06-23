53
47

I have a defect here 

Restore complete (1.2s)
  Weather.Core net10.0 succeeded (0.5s) → src\Weather.Core\bin\Debug\net10.0\Weather.Core.dll
  Weather.ServiceDefaults net10.0 succeeded (0.8s) → src\Weather.ServiceDefaults\bin\Debug\net10.0\Weather.ServiceDefaults.dll
  Weather.Infrastructure net10.0 failed with 1 error(s) (0.6s)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
  Weather.Core.Tests net10.0 succeeded (1.0s) → tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll
  Weather.Web net10.0 succeeded (1.5s) → src\Weather.Web\bin\Debug\net10.0\Weather.Web.dll
  Weather.Web.Tests net10.0 succeeded (0.9s) → tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll

Build failed with 1 error(s) in 4.7s
2026-06-23-18-35-50
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-54
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-59