$("header #search-button").on("click", () => {
	$("header").addClass("search-active")
})

$("header #back-button").on("click", () => {
	$("header").removeClass("search-active")
})
