const instances = new WeakMap();

window.profileImageCropper = {
  initialize: function (imageElement, dotNetRef, zoom, offsetX, offsetY) {
    if (!imageElement) {
      throw new Error("Missing image element");
    }

    if (typeof Cropper === "undefined") {
      throw new Error("Cropper.js is not loaded");
    }

    this.destroy(imageElement);

    const instance = {
      cropper: null,
      dotNetRef: dotNetRef || null,
      suppressNotify: false
    };

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
    instances.set(imageElement, instance);
  },

  setViewport: function (imageElement, zoom, offsetX, offsetY) {
    const instance = instances.get(imageElement);
    if (!instance || !instance.cropper) {
      return;
    }

    applyViewport(instance, zoom, offsetX, offsetY);
  },

  getCroppedSquarePngDataUrl: function (imageElement, outputSize) {
    const instance = instances.get(imageElement);
    if (!instance || !instance.cropper) {
      throw new Error("Cropper is not initialized");
    }

    const normalizedOutputSize = Math.max(64, Number(outputSize) || 512);
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

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
