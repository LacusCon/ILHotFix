//
//  UpdateApp.mm
//  Unity-iPhone
//
//  Created by LacusCon on 2019/4/4.
//

#import "UpdateApp.h"

@implementation NSObject (UpdateApp)

- (void)UpdateApp:(NSString *)appURL
{
    NSString *allURL = [@"itms-services://?action=download-manifest&url=" stringByAppendingString: appURL];
    [[UIApplication sharedApplication] openURL:[NSURL URLWithString: allURL]];
}
@end

#if defined(__cplusplus)
extern "C"{
#endif
    void IOS_UpdateApp(const char *cpszURL){
        NSString *pszURL = [NSString stringWithUTF8String: cpszURL];
        [NSObject UpdateApp: pszURL];
    }
#if defined(__cplusplus)
}
#endif
