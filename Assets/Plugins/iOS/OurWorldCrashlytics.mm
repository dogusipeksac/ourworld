#import <Foundation/Foundation.h>
#import <objc/message.h>

static NSString* OurWorldNSString(const char* str) {
    return str != NULL ? [NSString stringWithUTF8String:str] : @"";
}

static id OurWorldCrashlyticsInstance(void) {
    Class cls = NSClassFromString(@"FIRCrashlytics");
    if (cls == Nil) return nil;
    SEL sel = NSSelectorFromString(@"crashlytics");
    if (![cls respondsToSelector:sel]) return nil;
    return ((id (*)(id, SEL))objc_msgSend)(cls, sel);
}

extern "C" {

void OurWorld_CrashlyticsInit(void) {
    Class appCls = NSClassFromString(@"FIRApp");
    if (appCls != Nil) {
        SEL defaultSel = NSSelectorFromString(@"defaultApp");
        SEL configSel = NSSelectorFromString(@"configure");
        id existing = [appCls respondsToSelector:defaultSel]
            ? ((id (*)(id, SEL))objc_msgSend)(appCls, defaultSel)
            : nil;
        if (existing == nil && [appCls respondsToSelector:configSel]) {
            NSString* plistPath = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
            if (plistPath != nil) {
                ((void (*)(id, SEL))objc_msgSend)(appCls, configSel);
            } else {
                NSLog(@"[OurWorldCrashlytics] GoogleService-Info.plist not found in app bundle.");
            }
        }
    }

    id crashlytics = OurWorldCrashlyticsInstance();
    SEL enableSel = NSSelectorFromString(@"setCrashlyticsCollectionEnabled:");
    if (crashlytics != nil && [crashlytics respondsToSelector:enableSel]) {
        ((void (*)(id, SEL, BOOL))objc_msgSend)(crashlytics, enableSel, YES);
    }
}

void OurWorld_CrashlyticsLog(const char* message) {
    id crashlytics = OurWorldCrashlyticsInstance();
    SEL logSel = NSSelectorFromString(@"log:");
    if (crashlytics != nil && [crashlytics respondsToSelector:logSel]) {
        ((void (*)(id, SEL, NSString*))objc_msgSend)(crashlytics, logSel, OurWorldNSString(message));
    }
}

void OurWorld_CrashlyticsSetKey(const char* key, const char* value) {
    id crashlytics = OurWorldCrashlyticsInstance();
    SEL keySel = NSSelectorFromString(@"setCustomValue:forKey:");
    if (crashlytics != nil && [crashlytics respondsToSelector:keySel]) {
        ((void (*)(id, SEL, id, NSString*))objc_msgSend)(
            crashlytics, keySel, OurWorldNSString(value), OurWorldNSString(key));
    }
}

void OurWorld_CrashlyticsSetUserId(const char* userId) {
    id crashlytics = OurWorldCrashlyticsInstance();
    SEL userSel = NSSelectorFromString(@"setUserID:");
    if (crashlytics != nil && [crashlytics respondsToSelector:userSel]) {
        ((void (*)(id, SEL, NSString*))objc_msgSend)(crashlytics, userSel, OurWorldNSString(userId));
    }
}

void OurWorld_CrashlyticsRecordException(const char* name, const char* reason, const char* stackTrace) {
    id crashlytics = OurWorldCrashlyticsInstance();
    Class exCls = NSClassFromString(@"FIRExceptionModel");
    Class frameCls = NSClassFromString(@"FIRStackFrame");
    if (crashlytics == nil || exCls == Nil) return;

    NSString* exName = OurWorldNSString(name);
    NSString* exReason = OurWorldNSString(reason);
    NSString* trace = OurWorldNSString(stackTrace);

    SEL modelSel = NSSelectorFromString(@"exceptionModelWithName:reason:");
    if (![exCls respondsToSelector:modelSel]) return;
    id model = ((id (*)(id, SEL, NSString*, NSString*))objc_msgSend)(
        exCls,
        modelSel,
        exName.length > 0 ? exName : @"UnityException",
        exReason.length > 0 ? exReason : @"Unhandled C# exception");
    if (model == nil) return;

    if (frameCls != Nil) {
        SEL frameSel = NSSelectorFromString(@"stackFrameWithSymbol:file:line:");
        SEL setStackSel = NSSelectorFromString(@"setStackTrace:");
        if ([frameCls respondsToSelector:frameSel] && [model respondsToSelector:setStackSel]) {
            NSMutableArray* frames = [NSMutableArray array];
            NSArray<NSString*>* lines = [trace componentsSeparatedByCharactersInSet:[NSCharacterSet newlineCharacterSet]];
            for (NSString* line in lines) {
                NSString* trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
                if (trimmed.length == 0) continue;
                id frame = ((id (*)(id, SEL, NSString*, NSString*, NSInteger))objc_msgSend)(
                    frameCls, frameSel, trimmed, @"UnityCSharp", 0);
                if (frame != nil) {
                    [frames addObject:frame];
                }
                if (frames.count >= 64) break;
            }
            if (frames.count > 0) {
                ((void (*)(id, SEL, NSArray*))objc_msgSend)(model, setStackSel, frames);
            }
        }
    }

    SEL recordSel = NSSelectorFromString(@"recordExceptionModel:");
    if ([crashlytics respondsToSelector:recordSel]) {
        ((void (*)(id, SEL, id))objc_msgSend)(crashlytics, recordSel, model);
    }
}

void OurWorld_CrashlyticsForceTestCrash(void) {
    NSLog(@"[OurWorldCrashlytics] Forcing intentional test crash for Crashlytics verification.");
    __builtin_trap();
}

}
