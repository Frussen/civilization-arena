#import <AppKit/AppKit.h>

static id fullscreenShortcutMonitor;

static NSWindow *CivilizationArenaMainWindow()
{
    NSWindow *window = NSApp.keyWindow;
    if (window != nil)
    {
        return window;
    }

    window = NSApp.mainWindow;
    if (window != nil)
    {
        return window;
    }

    for (NSWindow *candidate in NSApp.orderedWindows)
    {
        if (candidate.isVisible &&
            (candidate.styleMask & NSWindowStyleMaskTitled) != 0)
        {
            return candidate;
        }
    }

    return nil;
}

static void CivilizationArenaRunOnMainThread(dispatch_block_t action)
{
    if (NSThread.isMainThread)
    {
        action();
        return;
    }

    dispatch_async(dispatch_get_main_queue(), action);
}

static void CivilizationArenaToggleNativeFullscreen()
{
    NSWindow *window = CivilizationArenaMainWindow();
    if (window != nil)
    {
        [window toggleFullScreen:nil];
    }
}

extern "C" __attribute__((visibility("default")))
void CivilizationArena_InstallNativeFullscreenShortcut()
{
    CivilizationArenaRunOnMainThread(^{
        if (fullscreenShortcutMonitor != nil)
        {
            return;
        }

        fullscreenShortcutMonitor =
            [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskKeyDown
                handler:^NSEvent *(NSEvent *event) {
                    NSEventModifierFlags modifiers =
                        event.modifierFlags &
                        NSEventModifierFlagDeviceIndependentFlagsMask;
                    BOOL isCommandF =
                        (modifiers & NSEventModifierFlagCommand) != 0 &&
                        (modifiers & (NSEventModifierFlagControl |
                                      NSEventModifierFlagOption |
                                      NSEventModifierFlagShift)) == 0 &&
                        [[event.charactersIgnoringModifiers lowercaseString]
                            isEqualToString:@"f"];

                    if (!isCommandF)
                    {
                        return event;
                    }

                    CivilizationArenaToggleNativeFullscreen();
                    return nil;
                }];
    });
}

extern "C" __attribute__((visibility("default")))
void CivilizationArena_SetNativeFullscreen(int enabled)
{
    CivilizationArenaRunOnMainThread(^{
        NSWindow *window = CivilizationArenaMainWindow();
        if (window == nil)
        {
            return;
        }

        bool isFullscreen =
            (window.styleMask & NSWindowStyleMaskFullScreen) != 0;
        if (isFullscreen != (enabled != 0))
        {
            [window toggleFullScreen:nil];
        }
    });
}
