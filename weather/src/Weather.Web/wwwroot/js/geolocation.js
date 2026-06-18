// Browser geolocation bridge for the Blazor dashboard.
//
// Design rule: this promise ALWAYS resolves — it never rejects. Every outcome
// (granted, denied, unavailable, timed out, unsupported) comes back as a plain
// object the C# side can branch on. That keeps "the user said no" on the normal
// path instead of the exception path.
//
// Resolved shape:
//   { success, latitude, longitude, accuracy, errorCode }
// where errorCode is one of:
//   "PermissionDenied" | "PositionUnavailable" | "Timeout" | "NotSupported" | null
window.weatherGeo = {
    getCurrentPosition: function () {
        return new Promise(function (resolve) {
            if (!navigator || !("geolocation" in navigator)) {
                resolve({
                    success: false,
                    latitude: null,
                    longitude: null,
                    accuracy: null,
                    errorCode: "NotSupported"
                });
                return;
            }

            navigator.geolocation.getCurrentPosition(
                function (position) {
                    resolve({
                        success: true,
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy,
                        errorCode: null
                    });
                },
                function (error) {
                    // 1 = PERMISSION_DENIED, 2 = POSITION_UNAVAILABLE, 3 = TIMEOUT
                    var code = "PositionUnavailable";
                    if (error && typeof error.code === "number") {
                        if (error.code === 1) {
                            code = "PermissionDenied";
                        } else if (error.code === 3) {
                            code = "Timeout";
                        } else {
                            code = "PositionUnavailable";
                        }
                    }
                    resolve({
                        success: false,
                        latitude: null,
                        longitude: null,
                        accuracy: null,
                        errorCode: code
                    });
                },
                {
                    enableHighAccuracy: false,
                    timeout: 10000,
                    maximumAge: 300000
                }
            );
        });
    }
};
