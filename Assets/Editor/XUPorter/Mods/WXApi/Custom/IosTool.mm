#import "IosTool.h"
#import "UnityAppController.h"
#import "WXApi.h"
#import "WXApiManager.h"

//const char *cpszUnityCallBackerGOName;3
//const char *cpszUnityCallBackerMethondName;

#if defined(__cplusplus)
extern "C"{
#endif
    void UnitySendMessage(const char *, const char *, const char *);
    NSString* _CreateNSString (const char* string);
#if defined(__cplusplus)
}
#endif

//************
@implementation IosTools

- (void)objc_copyTextToClipboard : (NSString*)text
{
    UIPasteboard *pasteboard = [UIPasteboard generalPasteboard];
    pasteboard.string = text;
}

- (void)AwakeOtherApp:(NSString *)pszURL
{
    //NSString *customURL = @"otherApp://";
    if ([[UIApplication sharedApplication]
         canOpenURL:[NSURL URLWithString:pszURL]])
    {
        [[UIApplication sharedApplication] openURL:[NSURL URLWithString:pszURL]];
    }
    else
    {
        UIAlertView *alert = [[UIAlertView alloc] initWithTitle:@"URL error"
                                                        message:[NSString stringWithFormat:
                                                                 @"No custom URL defined for %@", pszURL]
                                                       delegate:self cancelButtonTitle:@"Ok"
                                              otherButtonTitles:nil];
        [alert show];
    }
}

- (int )getSignalStrength{
    UIApplication *app = [UIApplication sharedApplication];
    NSArray *subviews = [[[app valueForKey:@"statusBar"] valueForKey:@"foregroundView"] subviews];
    UIView *dataNetworkItemView = nil;
    
    for (UIView * subview in subviews) {
        if([subview isKindOfClass:[NSClassFromString(@"UIStatusBarDataNetworkItemView") class]]) {
            dataNetworkItemView = subview;
            break;
        }
    }
    
    int signalStrength = [[dataNetworkItemView valueForKey:@"_wifiStrengthBars"] intValue];
    
    NSLog(@"signal %d", signalStrength);
    return signalStrength;
}

- (void)callPhone:(NSString *)phoneNumber
{
    //phoneNumber = "18369......"
    NSMutableString * str=[[NSMutableString alloc] initWithFormat:@"tel:%@",phoneNumber];
    [[UIApplication sharedApplication] openURL:[NSURL URLWithString:str]];
}

@end
//*************
#if defined(__cplusplus)
extern "C"{
#endif
    
    NSString *s_pcWakeupStr;
    static IosTools *iosClipboard;
    NSString *s_pcWeiXinAppid;
    NSString *s_pcUnitPluginGOName;
    
    NSString* _CreateNSString (const char* string)
    {
        if (string)
            return [NSString stringWithUTF8String: string];
        else
            return [NSString stringWithUTF8String: ""];
    }
    
    void IOS_CopyTextToClipboard(const char *textList)
    {
        NSString *text = [NSString stringWithUTF8String: textList] ;
        if(iosClipboard == NULL)
        {
            iosClipboard = [[IosTools alloc] init];
        }
        [iosClipboard objc_copyTextToClipboard: text];
    }
    
    void IOS_CallPhone(const char *   pszNumber){
        if(iosClipboard == NULL)
        {
            iosClipboard = [[IosTools alloc] init];
        }
        NSString *szNSNumber = [NSString stringWithUTF8String: pszNumber];
        [iosClipboard callPhone:szNSNumber];
    }
    
    float IOS_GetBatteryLevel()
    {
        [[UIDevice currentDevice] setBatteryMonitoringEnabled:YES];
        return [[UIDevice currentDevice] batteryLevel];
    }
    
    int IOS_GetSingleStrengh(){
        if(iosClipboard == NULL)
        {
            iosClipboard = [[IosTools alloc] init];
        }
        int nStrengh = [iosClipboard getSignalStrength];
        printf("GetSingleStrengh() nStrengh:%d.",nStrengh);
        return nStrengh;
    }
    
    void IOS_WeiXin_Pay(const char *  cpszAppID, const char *  cpszPartnerId, const char *  cszPrepayId, const char *  cpszNonceStr, const char *  cpszTimeStamp, const char *  cpszPackage, const char *  cpszSign){
        //WXNontaxPayReq req = [[WXNontaxPayReq alloc] init];
        ///NSLog(@"nonTaxPay() url：");
        //NSLog(text);
        PayReq *req = [[PayReq alloc] init];
        req.partnerId =[NSString stringWithUTF8String: cpszPartnerId];
        req.prepayId =[NSString stringWithUTF8String: cszPrepayId];
        req.package =[NSString stringWithUTF8String: cpszPackage];
        req.nonceStr =[NSString stringWithUTF8String: cpszNonceStr];
        req.timeStamp =atoi(cpszTimeStamp);
        req.sign =[NSString stringWithUTF8String: cpszSign];
        
        [WXApi sendReq:req];
        
        //SendAuthReq* req = [[SendAuthReq alloc] init];
        //req.scope = @"snsapi_userinfo"; // @"post_timeline,sns"
        //req.state = state;
        //[WXApi sendReq:req];
    }
    
    void IOS_WeiXin_Share(int nShareType, const char * cpszShareTitle, const char *  pszShareContent, const char *pszThumbImagePath, const char *cpszURL, int nShareTarget){
        //printf("IOS_WeiXin_Share() step1\n");
        printf("IOS_WeiXin_Share() step2-1 nShareType:%d nShareTarget:%d cpszShareTitle:%s content:%s\n", nShareType,nShareTarget, cpszShareTitle, pszShareContent);
        SendMessageToWXReq *req = [[SendMessageToWXReq alloc] init];
        if(1 == nShareType){
            printf("IOS_WeiXin_Share() step2-1 nShareType:%d\n", nShareType);
            req.text = [NSString stringWithUTF8String: pszShareContent] ;
            req.bText = YES;
            req.message=nil;
        }else if(2 == nShareType){
            printf("IOS_WeiXin_Share() step2-2 nShareType:%d\n", nShareType);
            WXMediaMessage *pcMsg = [WXMediaMessage message];
            pcMsg.title =[NSString stringWithUTF8String: cpszShareTitle] ;
            pcMsg.description =[NSString stringWithUTF8String: pszShareContent] ;
            WXWebpageObject *webPageObject = [WXWebpageObject object];
            webPageObject.webpageUrl =[NSString stringWithUTF8String: cpszURL] ;
            pcMsg.mediaObject = webPageObject;
            //req =[[SendMessageToWXReq alloc] init];
            req.bText = NO;
            req.message = pcMsg;
        }
        else if(3 == nShareType){
            NSString *pszNSFilePath =[NSString stringWithUTF8String: pszShareContent];
            NSData *imageData = [NSData dataWithContentsOfFile:pszNSFilePath];
            //UIImage *thumbImage = [UIImage imageNamed:@"res1thumb.png"];
            WXImageObject *ext = [WXImageObject object];
            ext.imageData = imageData;
            if(NULL == imageData){
                printf("IOS_WeiXin_Share() step2-2-1 if(NULL == imageData) pszShareContent:%s\n", pszShareContent);
            }
            NSString *pszNSTitle = NULL;
            if(NULL != cpszShareTitle){
                pszNSTitle = [NSString stringWithUTF8String: cpszShareTitle];
            }
            printf("IOS_WeiXin_Share() step2-3 nShareType:%d\n", nShareType);
            WXMediaMessage *pcMsg = [WXMediaMessage message];
            pcMsg.title =pszNSTitle;
            pcMsg.mediaObject = ext;
            printf("IOS_WeiXin_Share() step2-3-2\n");
            if(NULL != pszThumbImagePath){
                //[pcMsg setThumbImage:[UIImage imageNamed:[NSString stringWithUTF8String: pszThumbImagePath]]];
            }
            printf("IOS_WeiXin_Share() step2-3-3\n");
            //req =[[SendMessageToWXReq alloc] init];
            req.bText = NO;
            req.message = pcMsg;
            printf("IOS_WeiXin_Share() step2-3-4\n");
        }
        req.scene = nShareTarget;
        //GetMessageFromWXResp *resp = [[GetMessageFromWXResp alloc] init];
        //resp.bText = req.bText;
        //if (resp.bText)
        //    resp.text = req.text;
        //[WXApi sendResp:resp];
        int nRet = [WXApi sendReq:req];
        printf("IOS_WeiXin_Share() step3 nRet:%d\n",nRet);
    }
    
    void IOS_WeiXin_Login(){
        SendAuthReq* req = [[SendAuthReq alloc] init];
        req.scope = @"snsapi_userinfo";
        int nRet = [WXApi sendReq:req];
        printf("IOS_WeiXin_Login() step1 nRet:%d\n",nRet);
        
        //float fBattery = IOS_GetBatteryLevel();
        //int nSingleStrengh =IOS_GetSingleStrengh();
        //printf("IOS_WeiXin_Login() step2 fBattery:%f nSingleStrengh:%d.\n",fBattery, nSingleStrengh);
    }
    
    void IOS_WeiXin_Registe(const char *textList){
        //textList = "wxd930ea5d5a258f4f\n";
        s_pcWeiXinAppid =[NSString stringWithUTF8String:textList];
        //printf("IOS_WeiXin_Registe() WeiXinAppid:%s\n", textList);
        NSLog(@"IOS_WeiXin_Registe() nsstr:%@",s_pcWakeupStr);
        //wxd930ea5d5a258f4f
        // [WXApi registerApp:s_pcWeiXinAppid, enableMTA:YES];
        [WXApi registerApp:s_pcWeiXinAppid enableMTA:YES];
        //[WXApi enableMTA:YES];
        UInt64 typeFlag = MMAPP_SUPPORT_TEXT | MMAPP_SUPPORT_PICTURE | MMAPP_SUPPORT_LOCATION | MMAPP_SUPPORT_VIDEO |MMAPP_SUPPORT_AUDIO | MMAPP_SUPPORT_WEBPAGE | MMAPP_SUPPORT_DOC | MMAPP_SUPPORT_DOCX | MMAPP_SUPPORT_PPT | MMAPP_SUPPORT_PPTX | MMAPP_SUPPORT_XLS | MMAPP_SUPPORT_XLSX | MMAPP_SUPPORT_PDF;
        [WXApi registerAppSupportContentFlag:typeFlag];
    }
    
    void IOS_UnityPlugin_Registe(const char *textList){
        s_pcUnitPluginGOName =[NSString stringWithUTF8String:textList];;
        printf("IOS_UnityPlugin_Registe() GOName:%s", textList);
    }
    
    void IOS_Wakeup(const char *cpszURL){
        NSString *pszURL =[NSString stringWithUTF8String: cpszURL];
        if(iosClipboard == NULL)
        {
            iosClipboard = [[IosTools alloc] init];
        }
        [iosClipboard AwakeOtherApp: pszURL];
    }
    
    const char* IOS_GetWakeupStr(){
        if(NULL == s_pcWakeupStr){
            s_pcWakeupStr =[NSString stringWithUTF8String: "null"];
        }
        char *pszWakeupStr =NULL;
        NSUInteger length = [s_pcWakeupStr length] + 1;
        pszWakeupStr = (char*)malloc(length);
        memcpy(pszWakeupStr, [s_pcWakeupStr UTF8String], length);
        //RegisteToWeiXin("wxc39d2939fb835f14");
        //LoginToWeiXin("MainCamera", "OnWeixinShareRet");
        return pszWakeupStr;
    }
    
    
    
    /*
     void IOS_LoginToWeiXin(const char *cpszCallBackerGOName, const char *cpszCallBackerMethondName){
     cpszUnityCallBackerGOName = cpszCallBackerGOName;
     cpszUnityCallBackerMethondName = cpszCallBackerMethondName;
     SendAuthReq* req = [[SendAuthReq alloc] init];
     req.scope = @"snsapi_userinfo"; // @"post_timeline,sns"
     req.state = @"wechat_sdk";
     req.openID = s_pcWeiXinAppid;
     
     [WXApi sendAuthReq:req viewController:NULL delegate:[WXApiManager sharedManager]];
     }
     */
    
    
    
#if defined(__cplusplus)
}
#endif
