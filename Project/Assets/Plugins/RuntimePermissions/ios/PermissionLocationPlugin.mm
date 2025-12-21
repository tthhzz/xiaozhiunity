//
//  PermissionLocationPlugin.mm
//  Unity-iPhone
//https://github.com/react-native-community/react-native-permissions.git
//  Created by blitz on 2020/4/21.
//  Modified to combine WhenInUse and Always permissions
//

#import <CoreLocation/CoreLocation.h>

// 定义回调函数类型
typedef void (*PermissionStatusCallback)(int status);

// 定义权限类型枚举
typedef enum {
    LocationPermissionTypeWhenInUse = 0,
    LocationPermissionTypeAlways = 1
} LocationPermissionType;

@interface PNativeLocation : NSObject <CLLocationManagerDelegate>
+ (int)checkPermission:(LocationPermissionType)permissionType;
+ (int)requestPermission:(LocationPermissionType)permissionType callback:(PermissionStatusCallback)callback;
+ (PNativeLocation *)sharedInstance;
@property (nonatomic, strong) CLLocationManager *locationManager;
@property (nonatomic, assign) PermissionStatusCallback permissionCallback;
@end

@implementation PNativeLocation

// 单例实现
static PNativeLocation *_sharedInstance = nil;

+ (PNativeLocation *)sharedInstance {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        _sharedInstance = [[self alloc] init];
    });
    return _sharedInstance;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _locationManager = [[CLLocationManager alloc] init];
        _locationManager.delegate = self;
    }
    return self;
}

+ (int)checkPermission:(LocationPermissionType)permissionType {
    
    if (![CLLocationManager locationServicesEnabled]) {
        return 3;//服务没有开启
    }
    
    CLAuthorizationStatus status = [CLLocationManager authorizationStatus];
    switch (status) {
      case kCLAuthorizationStatusNotDetermined://未决定
        return 2;
      case kCLAuthorizationStatusRestricted://受限制
      case kCLAuthorizationStatusDenied://拒绝
        return 3;
      case kCLAuthorizationStatusAuthorizedWhenInUse://在利用的时候可以
      case kCLAuthorizationStatusAuthorizedAlways://总是可以
        return 1;
    }
    return 1;
}

+ (int)requestPermission:(LocationPermissionType)permissionType callback:(PermissionStatusCallback)callback {
    
    if (![CLLocationManager locationServicesEnabled]) {
        if (callback) {
            callback(3);
        }
        return 3;
    }
    
    CLAuthorizationStatus status = [CLLocationManager authorizationStatus];
    if (status != kCLAuthorizationStatusNotDetermined) {
        // 如果已经有确定的授权状态，直接返回当前状态
        int result;
        switch (status) {
            case kCLAuthorizationStatusRestricted:
            case kCLAuthorizationStatusDenied:
                result = 3;
                break;
            case kCLAuthorizationStatusAuthorizedWhenInUse:
            case kCLAuthorizationStatusAuthorizedAlways:
                result = 1;
                break;
            default:
                result = 2;
                break;
        }
        
        if (callback) {
            callback(result);
        }
        return result;
    }
    
    // 保存回调函数
    PNativeLocation *instance = [PNativeLocation sharedInstance];
    instance.permissionCallback = callback;
    
    // 根据权限类型申请不同的权限
    if (permissionType == LocationPermissionTypeWhenInUse) {
        [instance.locationManager requestWhenInUseAuthorization];
    } else {
        [instance.locationManager requestAlwaysAuthorization];
    }
    
    // 返回当前状态（未决定）
    return 2;
}

#pragma mark - CLLocationManagerDelegate

- (void)locationManager:(CLLocationManager *)manager didChangeAuthorizationStatus:(CLAuthorizationStatus)status {
    int result;
    switch (status) {
        case kCLAuthorizationStatusNotDetermined:
            result = 2;
            break;
        case kCLAuthorizationStatusRestricted:
        case kCLAuthorizationStatusDenied:
            result = 3;
            break;
        case kCLAuthorizationStatusAuthorizedWhenInUse:
        case kCLAuthorizationStatusAuthorizedAlways:
            result = 1;
            break;
    }
    
    // 调用回调函数
    if (self.permissionCallback) {
        self.permissionCallback(result);
        self.permissionCallback = nil; // 清除回调，防止多次调用
    }
}
@end

// 定义全局回调函数指针变量
static PermissionStatusCallback gPermissionCallback = NULL;

// C回调函数，将被传递给Objective-C代码
void NativePermissionCallback(int status) {
    if (gPermissionCallback != NULL) {
        gPermissionCallback(status);
        gPermissionCallback = NULL; // 清除回调，防止多次调用
    }
}

extern "C" int _PNativeLocation_CheckPermission(int permissionType) {
    return [PNativeLocation checkPermission:(LocationPermissionType)permissionType];
}

extern "C" int _PNativeLocation_RequestPermission(int permissionType, PermissionStatusCallback callback) {
    // 保存回调函数指针到全局变量
    gPermissionCallback = callback;
    
    // 调用Objective-C方法，传入C回调函数
    return [PNativeLocation requestPermission:(LocationPermissionType)permissionType callback:NativePermissionCallback];
}