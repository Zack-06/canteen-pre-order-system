$("header #search-button").on("click", () => {
	$("header").addClass("search-active")
})

$("header #back-button").on("click", () => {
	$("header").removeClass("search-active")
})

const toast_message = $(".toast-container").data("toast-message")
if (toast_message) {
	showToast(toast_message)
}

function showToast(message) {
	const toast = $('<div class="toast"></div>').text(message)
	$(".toast-container").append(toast)
	setTimeout(() => toast.addClass("show"), 100)
	setTimeout(() => {
		toast.removeClass("show")
		setTimeout(() => toast.remove(), 500)
	}, 4000)
}

function confirmation(question = "", okay = "Yes", cancel = "Cancel") {
	return new Promise((resolve) => {
		$("#confirmation-popup .title").text(question)
		$("#confirmation-popup-confirm").text(okay)
		$("#confirmation-popup-cancel").text(cancel)
		$("#confirmation-popup").addClass("show")
		$("#confirmation-popup-confirm").on("click", function () {
			$("#confirmation-popup").removeClass("show")
			resolve(true)
		})
		$("#confirmation-popup-cancel").on("click", function () {
			$("#confirmation-popup").removeClass("show")
			resolve(false)
		})
	})
}

window.confirmation = confirmation
