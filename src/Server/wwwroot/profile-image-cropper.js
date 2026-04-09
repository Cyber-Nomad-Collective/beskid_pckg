const instances = new WeakMap();

window.profileImageCropper = {
  initialize: function (imageElement, dotNetRef, zoom, offsetX, offsetY) {
    if (!imageElement) {
      throw new Error("Missing image element");
    }

    this.destroy(imageElement);

    const instance = {
      cropper: null,
      dotNetRef: dotNetRef || null,
      suppressNotify: false,
      fallbackMode: typeof Cropper === "undefined",
      viewport: {
        zoom: clamp(Number(zoom) || 1, 1, 3),
        offsetX: clamp(Number(offsetX) || 0, -1, 1),
        offsetY: clamp(Number(offsetY) || 0, -1, 1)
      }
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
        crop: function () {
          notifyCropChanged(instance);
        },
        ready: function () {
          applyViewport(instance, zoom, offsetX, offsetY);
        }
      });

      instance.cropper = cropper;
    } else {
      applyFallbackViewport(imageElement, instance.viewport.zoom, instance.viewport.offsetX, instance.viewport.offsetY);
      notifyFallbackChanged(instance);
    }

    instances.set(imageElement, instance);
  },

  setViewport: function (imageElement, zoom, offsetX, offsetY) {
    const instance = instances.get(imageElement);
    if (!instance) {
      return;
    }

    if (instance.fallbackMode) {
      instance.viewport.zoom = clamp(Number(zoom) || 1, 1, 3);
      instance.viewport.offsetX = clamp(Number(offsetX) || 0, -1, 1);
      instance.viewport.offsetY = clamp(Number(offsetY) || 0, -1, 1);
      applyFallbackViewport(imageElement, instance.viewport.zoom, instance.viewport.offsetX, instance.viewport.offsetY);
      notifyFallbackChanged(instance);
      return;
    }

    applyViewport(instance, zoom, offsetX, offsetY);
  },

  getCroppedSquarePngDataUrl: function (imageElement, outputSize) {
    const instance = instances.get(imageElement);
    if (!instance) {
      throw new Error("Cropper is not initialized");
    }

    const normalizedOutputSize = Math.max(64, Number(outputSize) || 512);

    if (instance.fallbackMode) {
      return renderFallbackCrop(imageElement, instance.viewport, normalizedOutputSize);
    }

    const canvas = instance.cropper.getCroppedCanvas({
      width: normalizedOutputSize,
      height: normalizedOutputSize,
      fillColor: "#ffffff"
    });

    if (!canvas) {
      throw new Error("Unable to render cropped image");
    }

    return canvas.toDataURL("image/png");
  },

  destroy: function (imageElement) {
    const instance = instances.get(imageElement);
    if (!instance) {
      return;
    }

    if (instance.cropper) {
      instance.cropper.destroy();
    }

    instances.delete(imageElement);
  }
};

function applyViewport(instance, zoom, offsetX, offsetY) {
  const cropper = instance.cropper;
  if (!cropper) {
    return;
  }

  const imageData = cropper.getImageData();
  const naturalWidth = Number(imageData.naturalWidth) || 0;
  const naturalHeight = Number(imageData.naturalHeight) || 0;
  if (naturalWidth <= 0 || naturalHeight <= 0) {
    return;
  }

  const normalizedZoom = clamp(Number(zoom) || 1, 1, 3);
  const normalizedOffsetX = clamp(Number(offsetX) || 0, -1, 1);
  const normalizedOffsetY = clamp(Number(offsetY) || 0, -1, 1);

  const minSide = Math.min(naturalWidth, naturalHeight);
  const cropSide = minSide / normalizedZoom;
  const maxX = Math.max(0, naturalWidth - cropSide);
  const maxY = Math.max(0, naturalHeight - cropSide);
  const x = ((normalizedOffsetX + 1) / 2) * maxX;
  const y = ((normalizedOffsetY + 1) / 2) * maxY;

  instance.suppressNotify = true;
  cropper.setData({
    x: x,
    y: y,
    width: cropSide,
    height: cropSide
  });
  instance.suppressNotify = false;
}

function notifyCropChanged(instance) {
  if (instance.suppressNotify || !instance.dotNetRef || !instance.cropper) {
    return;
  }

  const data = instance.cropper.getData(true);
  const imageData = instance.cropper.getImageData();
  const naturalWidth = Number(imageData.naturalWidth) || 0;
  const naturalHeight = Number(imageData.naturalHeight) || 0;
  const width = Number(data.width) || 0;
  const x = Number(data.x) || 0;
  const y = Number(data.y) || 0;

  if (naturalWidth <= 0 || naturalHeight <= 0 || width <= 0) {
    return;
  }

  const minSide = Math.min(naturalWidth, naturalHeight);
  const maxX = Math.max(0, naturalWidth - width);
  const maxY = Math.max(0, naturalHeight - width);

  const zoom = clamp(minSide / width, 1, 3);
  const offsetX = maxX > 0 ? clamp((x / maxX) * 2 - 1, -1, 1) : 0;
  const offsetY = maxY > 0 ? clamp((y / maxY) * 2 - 1, -1, 1) : 0;

  instance.dotNetRef.invokeMethodAsync("OnCropperChanged", zoom, offsetX, offsetY);
}

function applyFallbackViewport(imageElement, zoom, offsetX, offsetY) {
  const translateXPercent = -(offsetX * 18);
  const translateYPercent = -(offsetY * 18);
  imageElement.style.transformOrigin = "center center";
  imageElement.style.transform = `scale(${zoom}) translate(${translateXPercent}%, ${translateYPercent}%)`;
}

function notifyFallbackChanged(instance) {
  if (!instance.dotNetRef) {
    return;
  }

  instance.dotNetRef.invokeMethodAsync(
    "OnCropperChanged",
    instance.viewport.zoom,
    instance.viewport.offsetX,
    instance.viewport.offsetY);
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
  context.drawImage(imageElement, sourceX, sourceY, cropSide, cropSide, 0, 0, outputSize, outputSize);
  return canvas.toDataURL("image/png");
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
