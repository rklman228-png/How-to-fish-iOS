import SwiftUI

@main
struct HowFishControlsDemoApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
                .ignoresSafeArea()
                .persistentSystemOverlays(.hidden)
        }
    }
}
