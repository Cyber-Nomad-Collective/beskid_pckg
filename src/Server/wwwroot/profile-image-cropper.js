window.profileImageCropper = {
  cropSquareToPngDataUrl: async function (dataUrl, zoom, offsetX, offsetY, outputSize) {
    if (!dataUrl) {
      throw new Error("Missing source image");
    }

    const image = await loadImage(dataUrl);

    const normalizedZoom = Math.min(3, Math.max(1, Number(zoom) || 1));
    const normalizedOffsetX = Math.min(1, Math.max(-1, Number(offsetX) || 0));
    const normalizedOffsetY = Math.min(1, Math.max(-1, Number(offsetY) || 0));
    const normalizedOutputSize = Math.max(64, Number(outputSize) || 512);

    const minSide = Math.min(image.naturalWidth, image.naturalHeight);
    const cropSide = minSide / normalizedZoom;
    const maxX = Math.max(0, image.naturalWidth - cropSide);
    const maxY = Math.max(0, image.naturalHeight - cropSide);

    const sourceX = ((normalizedOffsetX + 1) / 2) * maxX;
    const sourceY = ((normalizedOffsetY + 1) / 2) * maxY;

    const canvas = document.createElement("canvas");
    canvas.width = normalizedOutputSize;
    canvas.height = normalizedOutputSize;

    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("Unable to create image context");
    }

    context.drawImage(
      image,
      sourceX,
      sourceY,
      cropSide,
      cropSide,
      0,
      0,
      normalizedOutputSize,
      normalizedOutputSize
    );

    return canvas.toDataURL("image/png");
  }
};

function loadImage(dataUrl) {
  return new Promise(function (resolve, reject) {
    const image = new Image();
    image.onload = function () { resolve(image); };
    image.onerror = function () { reject(new Error("Unable to load image")); };
    image.src = dataUrl;
  });
}
