// Klient-side nedskalering af billeder FØR upload, så payloaden holdes lille (hurtigere upload,
// mindre dataforbrug på mobil). Serveren nedskalerer/komprimerer ALTID igen som backstop, så
// dette er kun en optimering — fejler den, kan man trygt uploade originalen.

const MAX_DIM = 1024;   // længste led (px) — matcher serverens grænse
const QUALITY = 0.8;    // JPEG-kvalitet

function loadImage(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Billedet kunne ikke indlæses.'));
    img.src = src;
  });
}

function readAsDataUrl(file: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('Filen kunne ikke læses.'));
    reader.readAsDataURL(file);
  });
}

/**
 * Nedskalerer et billede til maks. 1024px på længste led og re-encoder som JPEG.
 * Returnerer en Blob klar til upload. Opskalerer aldrig små billeder.
 */
export async function downscaleImage(file: Blob): Promise<Blob> {
  const dataUrl = await readAsDataUrl(file);
  const img = await loadImage(dataUrl);

  let { width, height } = img;
  if (width > MAX_DIM || height > MAX_DIM) {
    const scale = Math.min(MAX_DIM / width, MAX_DIM / height);
    width = Math.round(width * scale);
    height = Math.round(height * scale);
  }

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas ikke understøttet.');
  ctx.drawImage(img, 0, 0, width, height);

  return new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      blob => (blob ? resolve(blob) : reject(new Error('Billedet kunne ikke komprimeres.'))),
      'image/jpeg',
      QUALITY,
    );
  });
}
