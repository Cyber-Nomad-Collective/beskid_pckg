const instances = new WeakMap();

window.profileImageCropper = {
	initialize: function (imageElement) {
		if (!imageElement) {
			throw new Error("Missing image element");
		}

		this.destroy(imageElement);

		const instance = {
			cropper: null,
			fallbackMode: typeof Cropper === "undefined",
			viewport: {
				zoom: 1,
				offsetX: 0,
				offsetY: 0,
			},
		};

		if (!instance.fallbackMode) {
			const cropper = new Cropper(imageElement, {
				aspectRatio: 1,
				viewMode: 1,
				dragMode: "move",
				autoCropArea: 1,
				responsive: true,
				background: false,
				scalable: false,
				rotatable: false,
				zoomable: true,
			});

			instance.cropper = cropper;
		} else {
			applyFallbackViewport(
				imageElement,
				instance.viewport.zoom,
				instance.viewport.offsetX,
				instance.viewport.offsetY,
			);
		}

		instances.set(imageElement, instance);
	},

	getCroppedSquarePngDataUrl: (imageElement, outputSize) => {
		const instance = instances.get(imageElement);
		if (!instance) {
			throw new Error("Cropper is not initialized");
		}

		const normalizedOutputSize = Math.max(64, Number(outputSize) || 512);

		if (instance.fallbackMode) {
			return renderFallbackCrop(
				imageElement,
				instance.viewport,
				normalizedOutputSize,
			);
		}

		const canvas = instance.cropper.getCroppedCanvas({
			width: normalizedOutputSize,
			height: normalizedOutputSize,
			fillColor: "#ffffff",
		});

		if (!canvas) {
			throw new Error("Unable to render cropped image");
		}

		return canvas.toDataURL("image/png");
	},

	destroy: (imageElement) => {
		const instance = instances.get(imageElement);
		if (!instance) {
			return;
		}

		if (instance.cropper) {
			instance.cropper.destroy();
		}

		instances.delete(imageElement);
	},
};

function applyFallbackViewport(imageElement, zoom, offsetX, offsetY) {
	const translateXPercent = -(offsetX * 18);
	const translateYPercent = -(offsetY * 18);
	imageElement.style.transformOrigin = "center center";
	imageElement.style.transform = `scale(${zoom}) translate(${translateXPercent}%, ${translateYPercent}%)`;
}

function renderFallbackCrop(imageElement, viewport, outputSize) {
	const naturalWidth = Number(imageElement.naturalWidth) || 0;
	const naturalHeight = Number(imageElement.naturalHeight) || 0;
	if (naturalWidth <= 0 || naturalHeight <= 0) {
		throw new Error("Image is not loaded");
	}

	const zoom = clamp(Number(viewport.zoom) || 1, 1, 3);
	const offsetX = clamp(Number(viewport.offsetX) || 0, -1, 1);
	const offsetY = clamp(Number(viewport.offsetY) || 0, -1, 1);
	const minSide = Math.min(naturalWidth, naturalHeight);
	const cropSide = minSide / zoom;
	const maxX = Math.max(0, naturalWidth - cropSide);
	const maxY = Math.max(0, naturalHeight - cropSide);
	const sourceX = ((offsetX + 1) / 2) * maxX;
	const sourceY = ((offsetY + 1) / 2) * maxY;

	const canvas = document.createElement("canvas");
	canvas.width = outputSize;
	canvas.height = outputSize;
	const context = canvas.getContext("2d");
	if (!context) {
		throw new Error("Canvas context is unavailable");
	}

	context.fillStyle = "#ffffff";
	context.fillRect(0, 0, outputSize, outputSize);
	context.drawImage(
		imageElement,
		sourceX,
		sourceY,
		cropSide,
		cropSide,
		0,
		0,
		outputSize,
		outputSize,
	);
	return canvas.toDataURL("image/png");
}

function clamp(value, min, max) {
	return Math.min(max, Math.max(min, value));
}
