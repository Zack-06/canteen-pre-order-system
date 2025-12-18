self.addEventListener("push", function (event) {
    event.waitUntil((async() => {
        try {
            const data = await event.data.json()
            const title = data.title || "Superchef"
            const body = data.body

            await self.registration.showNotification(title, {
                body: body,
                icon: "/img/logo/small.png",
                badge: "/img/logo/small.png",
                vibrate: [100, 50, 100],
                requireInteraction: true, // keeps the notification open until the user clicks on it
            })
        } catch(e) {}
    })())
})