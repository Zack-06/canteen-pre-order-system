// ==========Header Search==========
$("header #search-button").on("click", () => {
	$("header").addClass("search-active")
})

$("header #back-button").on("click", () => {
	$("header").removeClass("search-active")
})

// ==========Container Responsive Setup==========
function responsiveSetup() {
	const $responsiveContainer = $("[data-responsive-container]")
	if ($responsiveContainer.length !== 1) return

	$("main").removeClass("mobile")

	if ($responsiveContainer[0].scrollWidth > $responsiveContainer[0].clientWidth) {
		$("main").addClass("mobile")
	}
}
responsiveSetup()
window.addEventListener("resize", responsiveSetup)

// ==========Toast Message==========
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

// ==========Confirmation Popup==========
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

// ==========Password Toggle==========
$(".password-show-button").each(function () {
	$(this).attr("tabindex", -1)
})

$(".password-show-button").on("change", function () {
	$(this)
		.prev()
		.prop("type", $(this).prop("checked") ? "text" : "password")
})

// ==========Preserve Search Params==========
$("a[data-preserve-search-params]").each(function () {
	const $link = $(this)
	if (!$link.attr("href")) return

	const defaultURL = new URL($link.attr("href"), window.location.origin)
	const currentURL = new URL(window.location.href)
	for (const [key, value] of currentURL.searchParams) {
		defaultURL.searchParams.set(key, value)
	}
	$link.attr("href", defaultURL.toString())
})

// ==========Form Setup==========
$("form").prop("noValidate", true)
$("form").prop("autocomplete", "off")

// ==========Form Countdown==========
$("form[data-expired-timestamp]").each(function () {
	const $form = $(this)
	const expiryTimestamp = Number($form.attr("data-expired-timestamp"))

	$form.find("button[type='submit'], button:not([type])").each(function () {
		const $button = $(this)
		updateButtonCountdown($button, expiryTimestamp)

		let timer = setInterval(function () {
			updateButtonCountdown($button, expiryTimestamp, timer)
		}, 1000)
	})

	$form.find("[data-expired-disable]").each(function () {
		const $button = $(this)
		updateButtonCountdown($button, expiryTimestamp, null, false)

		let timer = setInterval(function () {
			updateButtonCountdown($button, expiryTimestamp, timer, false)
		}, 1000)
	})
})

$("button[data-expired-timestamp], [data-button][data-expired-timestamp]").each(function () {
	const $button = $(this)
	const expiryTimestamp = Number($button.attr("data-expired-timestamp"))
	updateButtonCountdown($button, expiryTimestamp)

	let timer = setInterval(function () {
		updateButtonCountdown($button, expiryTimestamp, timer)
	}, 1000)
})

function updateButtonCountdown($button, expiryTimestamp, timer, changeText = true) {
	let secondsLeft = Math.floor((expiryTimestamp - Date.now()) / 1000)

	if (!$button.attr("data-original-text")) {
		$button.attr("data-original-text", $button.text())
	}

	if (secondsLeft < 0) {
		$button.attr("disabled", true)
		if ($button.attr("data-button")) {
			$button.attr("data-button", "disabled")
		}

		if (changeText) {
			$button.text("Expired")
		}
		if (timer) {
			clearInterval(timer)
		}
		return
	}

	if (changeText) {
		const minutes = String(Math.floor(secondsLeft / 60)).padStart(2, "0")
		const seconds = String(secondsLeft % 60).padStart(2, "0")

		$button.text(`${$button.attr("data-original-text")} (${minutes}:${seconds})`)
	}
}

// ==========Clear Form==========
$("[data-clear-form]").on("click", function () {
	betterClearForm(this)
})
function betterClearForm(element) {
	const $form = $(element).closest("form")
	if (!$form.length) return

	$form
		.find("input, select, textarea")
		.not("[data-clear-form-ignore]")
		.each(function () {
			if (this.disabled || this.readOnly) return

			switch (this.type) {
				case "checkbox":
				case "radio":
					this.checked = false
					break
				case "select-one":
				case "select-multiple":
					this.selectedIndex = -1 // clears selection
					break
				default:
					this.value = ""
			}
		})
}

// ==========Auto Submit Form==========
$("[data-auto-submit]").on("change", function () {
	if ($(this).data("auto-submit") == "clear") {
		betterClearForm(this)
	}
    
	$(this).closest("form").submit()
})

// ==========Detect Form Changes==========
function detectFormChanges(formSelector, selectElements = "input", changedCallback = () => {}, unchangedCallback = () => {}) {
	let form = $(formSelector)
	let original_data = form.serialize()

	$(`${formSelector}`).on("change input", selectElements, function () {
		if (form.serialize() === original_data) {
			unchangedCallback()
		} else {
			changedCallback()
		}
	})

	return function () {
		form = $(formSelector)
		original_data = form.serialize()
		unchangedCallback()
	}
}

// ==========Input Number==========
$("input[type='number']").on("keypress", function (event) {
	if (event.code === "KeyE") {
		event.preventDefault()
	}
})

// ==========Input Phone Number==========
$("input[data-phone]").each(function () {
	$(this).data("previous-value", $(this).val())
})
$("input[data-phone]").on("input", function () {
	let value = $(this).val()
	value = value.replace(/\D/g, "")
	const rawValue = value

	if (value.length > 3) {
		value = value.slice(0, 3) + "-" + value.slice(3)
	}

	let cursorPosition = this.selectionStart
	if (rawValue.length > $(this).data("previous-value").length) {
		const diff = rawValue.length - $(this).data("previous-value").length
		if (cursorPosition - diff === 3) {
			cursorPosition++
		}
	}

	$(this).val(value)
	$(this).data("previous-value", rawValue)
	this.setSelectionRange(cursorPosition, cursorPosition)
})

// ==========Input RM==========
$("input[data-rm-format]").on("input", function () {
	const value = $(this)
		.val()
		.replace(/[^0-9]/g, "")
	if (value !== "") {
		$(this).val(formatCents(value))
	}

	if ($(this).attr("data-rm-format") == "nullable") {
		if (Number(value) === 0) {
			$(this).val(null)
		}
	}
})

function ensureTwoDigitsAfterDecimal(value) {
	const decimalString = value.toString()
	const decimalPointLoc = decimalString.indexOf(".")

	if (decimalPointLoc !== -1) {
		const numberOfDecimalDigits = decimalString.length - (decimalPointLoc + 1)
		if (numberOfDecimalDigits === 1) {
			return decimalString + "0"
		}
		return decimalString
	} else {
		return decimalString + ".00"
	}
}

function formatCents(cents) {
	if (cents !== "") {
		return ensureTwoDigitsAfterDecimal(cents / 100)
	} else {
		return ""
	}
}

// ==========Formatting==========
function toRMFormat(value) {
	return value.toFixed(2)
}

function toRatingFormat(value) {
	return value.toFixed(1)
}

function toDateFormat(dateStr) {
	const months = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]
	const [year, month, day] = dateStr.split("-")
	return `${day} ${months[parseInt(month) - 1]} ${year}`
}

function timeAgo(datetime) {
	const formattedDatetime = datetime.replace(" ", "T")
	const now = new Date()
	const past = new Date(formattedDatetime)

	if (isNaN(past)) return "Invalid date"

	const seconds = Math.floor((now - past) / 1000)

	const intervals = {
		year: 31536000,
		month: 2592000,
		week: 604800,
		day: 86400,
		hour: 3600,
		minute: 60,
		second: 1
	}

	for (const unit in intervals) {
		const count = Math.floor(seconds / intervals[unit])
		if (count >= 1) {
			return `${count} ${unit}${count > 1 ? "s" : ""} ago`
		}
	}

	return "just now"
}

// ==========Overlay Setup==========
function openOverlay(id, callback = () => {}) {
	const $overlay = $(`#${id}`)
	if (!$overlay.length) return

	$overlay.addClass("show")
	$overlay.find(".overlay-close").off("click")
	$overlay.find(".overlay-close").on("click", function () {
		$overlay.removeClass("show")
		callback()
	})
}

function closeOverlay(id) {
	const $overlay = $(`#${id}`)
	if (!$overlay.length) return

	$overlay.removeClass("show")
}

// ==========Upload Overlay==========
class UploadOverlay {
	constructor(file_input_selector, outputWidth = 250, outputHeight = 250, upload_overlay_selector = "#upload-overlay") {
		this.file_input_selector = file_input_selector
		this.upload_overlay_selector = upload_overlay_selector
		this.outputWidth = outputWidth
		this.outputHeight = outputHeight
		this.storedFiles = []

		// Drag-over effects
		$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).on("dragover", (event) => {
			event.preventDefault()
			$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).addClass("drag-over")
		})

		$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).on("dragleave", (event) => {
			event.preventDefault()
			$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).removeClass("drag-over")
		})

		// Click to open file input
		$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).on("click", () => {
			$(this.file_input_selector).val("").click()
		})

		$(this.file_input_selector).on("click", (event) => event.stopPropagation())
	}

	assignFile() {
		$(this.file_input_selector).val("")
		if (this.storedFiles.length === 0) return
		const dataTransfer = new DataTransfer()
		this.storedFiles.forEach((file) => dataTransfer.items.add(file))
		$(this.file_input_selector)[0].files = dataTransfer.files
	}

	removeFile(index) {
		if (index >= 0 && index < this.storedFiles.length) {
			this.storedFiles.splice(index, 1)
			this.assignFile()
		}
	}

	removeAllFiles() {
		this.storedFiles = []
		this.assignFile()
	}

	open() {
		const previewContainer = $(`${this.upload_overlay_selector}.upload-overlay .upload-preview-container`)
		const aspectRatio = this.outputWidth / this.outputHeight

		// Fit overlay container
		fitUploadOverlayToScreen(previewContainer[0], aspectRatio)
		window.addEventListener("resize", () => fitUploadOverlayToScreen(previewContainer[0], aspectRatio))

		return new Promise((resolve) => {
			const handleFile = (file) => {
				if (file && file.type.startsWith("image/") && (file.type.endsWith("jpeg") || file.type.endsWith("png"))) {
					const reader = new FileReader()
					reader.onload = (event) => {
						const previewImage = $(`${this.upload_overlay_selector}.upload-overlay .upload-preview-image`)
						previewImage.attr("src", event.target.result)

						previewImage.off("load").on("load", () => {
							$(`${this.upload_overlay_selector}.upload-overlay .upload-preview-zone`).addClass("show")
							$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).removeClass("show")

							const imageRatio = previewImage.width() / previewImage.height()
							const containerRatio = previewContainer.width() / previewContainer.height()
							let horizontalFit = false

							if (imageRatio < containerRatio) {
								horizontalFit = true
								previewImage.addClass("horizontal-fit").removeClass("vertical-fit")
							} else {
								previewImage.addClass("vertical-fit").removeClass("horizontal-fit")
							}

							previewImage.css("transform", `translate(-50%, -50%) scale(1)`)

							let imgX = 0 // %
							let imgY = 0 // %
							let scale = 1

							// ===== Drag & Touch Support =====
							const startDrag = (evt) => {
								evt.preventDefault()
								const startX = evt.type.startsWith("touch") ? evt.touches[0].clientX : evt.clientX
								const startY = evt.type.startsWith("touch") ? evt.touches[0].clientY : evt.clientY
								const startImgX = imgX
								const startImgY = imgY

								previewImage.addClass("dragging")

								const moveHandler = (ev) => {
									const clientX = ev.type.startsWith("touch") ? ev.touches[0].clientX : ev.clientX
									const clientY = ev.type.startsWith("touch") ? ev.touches[0].clientY : ev.clientY

									const deltaX = clientX - startX
									const deltaY = clientY - startY

									imgX = startImgX + (deltaX / previewContainer.width()) * 100
									imgY = startImgY + (deltaY / previewContainer.height()) * 100

									const maxX = ((previewImage.width() * scale - previewContainer.width()) / previewContainer.width()) * 50
									const maxY = ((previewImage.height() * scale - previewContainer.height()) / previewContainer.height()) * 50

									imgX = Math.min(maxX, Math.max(-maxX, imgX))
									imgY = Math.min(maxY, Math.max(-maxY, imgY))

									previewImage.css("transform", `translate(calc(-50% + ${imgX}%), calc(-50% + ${imgY}%)) scale(${scale})`)
								}

								const endHandler = () => {
									previewImage.removeClass("dragging")
									$(document.body).off("mousemove touchmove", moveHandler)
									$(document.body).off("mouseup touchend", endHandler)
								}

								$(document.body).on("mousemove touchmove", moveHandler)
								$(document.body).on("mouseup touchend", endHandler)
							}

							previewImage.off("mousedown touchstart").on("mousedown touchstart", startDrag)

							// ===== Pinch to Zoom (Mobile) + Slider Zoom (Desktop) =====
							let initialDistance = null
							let initialScale = scale

							previewContainer[0].addEventListener(
								"touchstart",
								(e) => {
									if (e.touches.length === 2) {
										e.preventDefault()
										const dx = e.touches[0].clientX - e.touches[1].clientX
										const dy = e.touches[0].clientY - e.touches[1].clientY
										initialDistance = Math.sqrt(dx * dx + dy * dy)
										initialScale = scale
									}
								},
								{ passive: false }
							)

							previewContainer[0].addEventListener(
								"touchmove",
								(e) => {
									if (e.touches.length === 2 && initialDistance) {
										e.preventDefault()
										const dx = e.touches[0].clientX - e.touches[1].clientX
										const dy = e.touches[0].clientY - e.touches[1].clientY
										const distance = Math.sqrt(dx * dx + dy * dy)
										const newScale = initialScale * (distance / initialDistance)

										const scaleRatio = newScale / scale
										scale = newScale
										imgX *= scaleRatio
										imgY *= scaleRatio

										const maxX = ((previewImage.width() * scale - previewContainer.width()) / previewContainer.width()) * 50
										const maxY = ((previewImage.height() * scale - previewContainer.height()) / previewContainer.height()) * 50

										imgX = Math.min(maxX, Math.max(-maxX, imgX))
										imgY = Math.min(maxY, Math.max(-maxY, imgY))

										previewImage.css("transform", `translate(calc(-50% + ${imgX}%), calc(-50% + ${imgY}%)) scale(${scale})`)
									}
								},
								{ passive: false }
							)

							previewContainer[0].addEventListener("touchend", (e) => {
								if (e.touches.length < 2) initialDistance = null
							})

							// Desktop zoom slider
							$(".zoom-slider").val(1)
							$(".zoom-slider")
								.off("input")
								.on("input", (evt) => {
									const prevScale = scale
									scale = parseFloat(evt.target.value)

									const scaleRatio = scale / prevScale
									imgX *= scaleRatio
									imgY *= scaleRatio

									const maxX = ((previewImage.width() * scale - previewContainer.width()) / previewContainer.width()) * 50
									const maxY = ((previewImage.height() * scale - previewContainer.height()) / previewContainer.height()) * 50

									imgX = Math.min(maxX, Math.max(-maxX, imgX))
									imgY = Math.min(maxY, Math.max(-maxY, imgY))

									previewImage.css("transform", `translate(calc(-50% + ${imgX}%), calc(-50% + ${imgY}%)) scale(${scale})`)
								})

							// Confirm upload
							$(".confirm-upload")
								.off("click")
								.on("click", () => {
									$(`${this.upload_overlay_selector}.upload-overlay`).removeClass("show")
									this.storedFiles.push(file)
									this.assignFile()
									resolve({
										src: event.target.result,
										scale: scale,
										imgX: imgX,
										imgY: imgY,
										horizontalFit: horizontalFit,
										index: this.storedFiles.length - 1
									})
								})
						})
					}

					reader.readAsDataURL(file)
				}
			}

			const overlay = $(`${this.upload_overlay_selector}.upload-overlay`)
			$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).addClass("show")
			overlay.addClass("show")
			$(`${this.upload_overlay_selector}.upload-overlay .upload-preview-zone`).removeClass("show")

			// Close overlay
			overlay
				.find(".overlay-close")
				.off("click")
				.on("click", () => {
					overlay.removeClass("show")
					this.assignFile()
					resolve(false)
				})

			// File input change
			$(this.file_input_selector)
				.off("change")
				.on("change", (e) => {
					handleFile(e.target.files[0])
				})

			// Drop event
			$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`)
				.off("drop")
				.on("drop", (event) => {
					event.preventDefault()
					$(`${this.upload_overlay_selector}.upload-overlay .upload-drop-zone`).removeClass("drag-over")
					handleFile(event.originalEvent.dataTransfer.files[0])
				})
		})
	}
}

function fitUploadOverlayToScreen(uploadOverlay, aspectRatio) {
	const vw = window.innerWidth * 0.8 // max 80% of viewport width
	const vh = window.innerHeight * 0.6 // max 60% of viewport height

	let width = vw
	let height = width / aspectRatio

	// If height exceeds viewport, scale by height instead
	if (height > vh) {
		height = vh
		width = height * aspectRatio
	}

	uploadOverlay.style.width = width + "px"
	uploadOverlay.style.height = height + "px"
}

function updateImagePreview(selector, xPercent, yPercent, scale) {
	$(selector).css("transform", `translate(calc(-50% + ${xPercent}%), calc(-50% + ${yPercent}%)) scale(${scale})`)
}

// ==========Ajax Setup==========
function defaultOnFailure(xhr, status, error) {
	showToast(xhr.responseText)
}

function reloadPage() {
	window.location.reload()
}

function redirectToPage(url) {
	window.location.href = url
}

// ==========Custom Select==========
$(".custom-select").each(function () {
	let $this = $(this)
	let $select = $this.find("select")
	let $selected = $("<div>", { class: "select-selected" }).text($select.find("option:selected").text())
	let $items = $("<div>", { class: "select-items" })

	// Build option list
	$select.find("option").each(function (i) {
		let $opt = $("<div>").text($(this).text()).attr("data-value", $(this).val())

		if ($(this).attr("selected")) {
			$opt.addClass("same-as-selected")
		}
		$opt.on("click", function () {
			$select.val($(this).attr("data-value")) // update real select tag
			$selected.text($(this).text()) // update displayed text
			setPlaceholder($select)
			$items.find(".same-as-selected").removeClass("same-as-selected")
			$(this).addClass("same-as-selected")
			$items.hide()
			$selected.removeClass("select-arrow-active")
			$select.trigger("change")
		})
		$items.append($opt)
	})

	$this.append($selected)
	$this.append($items)

	// toggle dropdown
	$selected.on("click", function (e) {
		e.stopPropagation()
		$(".select-items").not($items).hide()
		$(".select-selected").not(this).removeClass("select-arrow-active")
		$items.toggle()
		$(this).toggleClass("select-arrow-active")
	})
})

$(document).on("click", function () {
	// Close dropdown if click outside
	$(".select-items").hide()
	$(".select-selected").removeClass("select-arrow-active")
})

// ==========Manage Container Search==========
function setPlaceholder(selectElement) {
	var selectedOption = selectElement.find(":selected")
	var searchInput = selectElement.closest("[data-manage-container='query']").find(".form-input input")
	if (searchInput.length == 0 || selectedOption.length == 0) return

	searchInput.attr("placeholder", selectedOption.text())
}
$("[data-manage-container='query'] select").each(function () {
	setPlaceholder($(this))
})
