#import <Foundation/Foundation.h>

#if __has_include(<FirebaseCore/FirebaseCore.h>)
#import <FirebaseCore/FirebaseCore.h>
#define OURWORLD_HAS_FIREBASE_CORE 1
#else
#define OURWORLD_HAS_FIREBASE_CORE 0
#endif

#if __has_include(<FirebaseCrashlytics/FirebaseCrashlytics.h>)
#import <FirebaseCrashlytics/FirebaseCrashlytics.h>
#define OURWORLD_HAS_FIREBASE_CRASHLYTICS 1
#else
#define OURWORLD_HAS_FIREBASE_CRASHLYTICS 0
#endif

static NSString* OurWorldNSString(const char* str) {
    return str != NULL ? [NSString stringWithUTF8String:str] : @"";
}

extern "C" {

void OurWorld_CrashlyticsInit(void) {
#if OURWORLD_HAS_FIREBASE_CORE
    if ([FIRApp defaultApp] == nil) {
        NSString* plistPath = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
        if (plistPath != nil) {
            [FIRApp configure];
        } else {
            NSLog(@"[OurWorldCrashlytics] GoogleService-Info.plist not found in app bundle.");
        }
    }
#endif
#if OURWORLD_HAS_FIREBASE_CRASHLYTICS
    [[FIRCrashlytics crashlytics] setCrashlyticsCollectionEnabled:YES];
#endif
}

void OurWorld_CrashlyticsLog(const char* message) {
#if OURWORLD_HAS_FIREBASE_CRASHLYTICS
    [[FIRCrashlytics crashlytics] log:OurWorldNSString(message)];
#endif
}

void OurWorld_CrashlyticsSetKey(const char* key, const char* value) {
#if OURWORLD_HAS_FIREBASE_CRASHLYTICS
    [[FIRCrashlytics crashlytics] setCustomValue:OurWorldNSString(value) forKey:OurWorldNSString(key)];
#endif
}

void OurWorld_CrashlyticsSetUserId(const char* userId) {
#if OURWORLD_HAS_FIREBASE_CRASHLYTICS
    [[FIRCrashlytics crashlytics] setUserID:OurWorldNSString(userId)];
#endif
}

void OurWorld_CrashlyticsRecordException(const char* name, const char* reason, const char* stackTrace) {
#if OURWORLD_HAS_FIREBASE_CRASHLYTICS
    NSString* exName = OurWorldNSString(name);
    NSString* exReason = OurWorldNSString(reason);
    NSString* trace = OurWorldNSString(stackTrace);

    FIRExceptionModel* model = [FIRExceptionModel exceptionModelWithName:(exName.length > 0 ? exName : @"UnityException")
                                                                  reason:(exReason.length > 0 ? exReason : @"Unhandled C# exception")];
    NSMutableArray<FIRStackFrame*>* frames = [NSMutableArray array];
    NSArray<NSString*>* lines = [trace componentsSeparatedByCharactersInSet:[NSCharacterSet newlineCharacterSet]];
    for (NSString* line in lines) {
        NSString* trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
        if (trimmed.length == 0) continue;
        FIRStackFrame* frame = [FIRStackFrame stackFrameWithSymbol:trimmed file:@"UnityCSharp" line:0];
        if (frame != nil) {
            [frames addObject:frame];
        }
        if (frames.count >= 64) break;
    }
    if (frames.count > 0) {
        model.stackTrace = frames;
    }
    [[FIRCrashlytics crashlytics] recordExceptionModel:model];
#endif
}

void OurWorld_CrashlyticsForceTestCrash(void) {
    NSLog(@"[OurWorldCrashlytics] Forcing intentional test crash for Crashlytics verification.");
    @throw [NSException exceptionWithName:@"OurWorldTestCrash"
                                   reason:@"Intentional Crashlytics verification test crash"
                                 userInfo:nil];
}

}
