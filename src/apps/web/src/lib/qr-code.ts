/**
 * Dependency-free QR Code generator (byte mode, versions 1-40).
 *
 * Derived from Project Nayuki's "QR Code generator library" (MIT License).
 * Trimmed to byte-mode text encoding with automatic version + mask selection,
 * which is all the treasure-hunt QR hashes need. Produces a boolean module
 * matrix that callers render (e.g. as SVG). No network, no npm dependency.
 */

export type Ecc = "L" | "M" | "Q" | "H";

const ECC_FORMAT_BITS: Record<Ecc, number> = { L: 1, M: 0, Q: 3, H: 2 };
const ECC_ORDER: Ecc[] = ["L", "M", "Q", "H"];

// Number of error-correction codewords, indexed by [eccOrdinal][version].
// Index 0 of each row is a placeholder so version can index directly.
const ECC_CODEWORDS_PER_BLOCK: number[][] = [
  // L
  [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
  // M
  [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
  // Q
  [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
  // H
  [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
];

const NUM_ECC_BLOCKS: number[][] = [
  // L
  [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
  // M
  [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
  // Q
  [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
  // H
  [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
];

function getNumRawDataModules(ver: number): number {
  let result = (16 * ver + 128) * ver + 64;
  if (ver >= 2) {
    const numAlign = Math.floor(ver / 7) + 2;
    result -= (25 * numAlign - 10) * numAlign - 55;
    if (ver >= 7) result -= 36;
  }
  return result;
}

function getNumDataCodewords(ver: number, ecc: Ecc): number {
  const eccOrd = ECC_ORDER.indexOf(ecc);
  return (
    Math.floor(getNumRawDataModules(ver) / 8) -
    ECC_CODEWORDS_PER_BLOCK[eccOrd][ver] * NUM_ECC_BLOCKS[eccOrd][ver]
  );
}

// --- Reed-Solomon over GF(256) ---
function reedSolomonComputeDivisor(degree: number): number[] {
  const result: number[] = new Array(degree).fill(0);
  result[degree - 1] = 1;
  let root = 1;
  for (let i = 0; i < degree; i++) {
    for (let j = 0; j < result.length; j++) {
      result[j] = reedSolomonMultiply(result[j], root);
      if (j + 1 < result.length) result[j] ^= result[j + 1];
    }
    root = reedSolomonMultiply(root, 0x02);
  }
  return result;
}

function reedSolomonComputeRemainder(data: number[], divisor: number[]): number[] {
  const result: number[] = new Array(divisor.length).fill(0);
  for (const b of data) {
    const factor = b ^ (result.shift() as number);
    result.push(0);
    for (let i = 0; i < divisor.length; i++) {
      result[i] ^= reedSolomonMultiply(divisor[i], factor);
    }
  }
  return result;
}

function reedSolomonMultiply(x: number, y: number): number {
  let z = 0;
  for (let i = 7; i >= 0; i--) {
    z = (z << 1) ^ ((z >>> 7) * 0x11d);
    z ^= ((y >>> i) & 1) * x;
  }
  return z & 0xff;
}

type Bit = 0 | 1;

function appendBits(value: number, len: number, bits: Bit[]): void {
  for (let i = len - 1; i >= 0; i--) {
    bits.push(((value >>> i) & 1) as Bit);
  }
}

/**
 * Encodes `text` (UTF-8, byte mode) into a QR module matrix.
 * Returns a square array of booleans (true = dark module).
 */
export function encodeQrMatrix(text: string, ecc: Ecc = "M"): boolean[][] {
  const dataBytes = Array.from(new TextEncoder().encode(text));

  // Pick the smallest version that fits in byte mode.
  let version = 1;
  for (; version <= 40; version++) {
    const capacityBits = getNumDataCodewords(version, ecc) * 8;
    const charCountBits = version < 10 ? 8 : 16;
    const usedBits = 4 + charCountBits + dataBytes.length * 8;
    if (usedBits <= capacityBits) break;
  }
  if (version > 40) {
    throw new Error("Datos demasiado largos para un código QR.");
  }

  const charCountBits = version < 10 ? 8 : 16;
  const bits: Bit[] = [];
  appendBits(0x4, 4, bits); // byte mode indicator
  appendBits(dataBytes.length, charCountBits, bits);
  for (const b of dataBytes) appendBits(b, 8, bits);

  const dataCapacityBits = getNumDataCodewords(version, ecc) * 8;
  appendBits(0, Math.min(4, dataCapacityBits - bits.length), bits); // terminator
  while (bits.length % 8 !== 0) bits.push(0);
  for (let pad = 0xec; bits.length < dataCapacityBits; pad ^= 0xec ^ 0x11) {
    appendBits(pad, 8, bits);
  }

  // Bits -> data codewords.
  const dataCodewords: number[] = new Array(bits.length / 8).fill(0);
  bits.forEach((bit, i) => {
    dataCodewords[i >>> 3] |= bit << (7 - (i & 7));
  });

  const matrix = buildMatrix(version, ecc, dataCodewords);
  return matrix;
}

function buildMatrix(version: number, ecc: Ecc, dataCodewords: number[]): boolean[][] {
  const size = version * 4 + 17;
  const modules: boolean[][] = Array.from({ length: size }, () => new Array(size).fill(false));
  const isFunction: boolean[][] = Array.from({ length: size }, () => new Array(size).fill(false));

  const setFunction = (x: number, y: number, dark: boolean) => {
    modules[y][x] = dark;
    isFunction[y][x] = true;
  };

  // Timing patterns.
  for (let i = 0; i < size; i++) {
    setFunction(6, i, i % 2 === 0);
    setFunction(i, 6, i % 2 === 0);
  }

  // Finder patterns + separators.
  const drawFinder = (cx: number, cy: number) => {
    for (let dy = -4; dy <= 4; dy++) {
      for (let dx = -4; dx <= 4; dx++) {
        const dist = Math.max(Math.abs(dx), Math.abs(dy));
        const x = cx + dx;
        const y = cy + dy;
        if (x >= 0 && x < size && y >= 0 && y < size) {
          setFunction(x, y, dist !== 2 && dist !== 4);
        }
      }
    }
  };
  drawFinder(3, 3);
  drawFinder(size - 4, 3);
  drawFinder(3, size - 4);

  // Alignment patterns.
  const alignPositions = getAlignmentPatternPositions(version);
  const numAlign = alignPositions.length;
  for (let i = 0; i < numAlign; i++) {
    for (let j = 0; j < numAlign; j++) {
      if (
        (i === 0 && j === 0) ||
        (i === 0 && j === numAlign - 1) ||
        (i === numAlign - 1 && j === 0)
      ) {
        continue; // overlaps finder patterns
      }
      const cx = alignPositions[i];
      const cy = alignPositions[j];
      for (let dy = -2; dy <= 2; dy++) {
        for (let dx = -2; dx <= 2; dx++) {
          setFunction(cx + dx, cy + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1);
        }
      }
    }
  }

  // Reserve format + version info areas (drawn after masking).
  reserveFormatInfo(size, setFunction, version);

  // Interleave ECC and place data.
  const allCodewords = addEccAndInterleave(version, ecc, dataCodewords);
  placeCodewords(modules, isFunction, size, allCodewords);

  // Pick best mask.
  let bestMask = 0;
  let minPenalty = Infinity;
  for (let mask = 0; mask < 8; mask++) {
    applyMask(modules, isFunction, size, mask);
    drawFormatBits(modules, isFunction, size, ecc, mask);
    const penalty = computePenalty(modules, size);
    if (penalty < minPenalty) {
      minPenalty = penalty;
      bestMask = mask;
    }
    applyMask(modules, isFunction, size, mask); // undo (XOR again)
  }
  applyMask(modules, isFunction, size, bestMask);
  drawFormatBits(modules, isFunction, size, ecc, bestMask);
  drawVersionInfo(modules, isFunction, size, version);

  return modules;
}

function getAlignmentPatternPositions(version: number): number[] {
  if (version === 1) return [];
  const numAlign = Math.floor(version / 7) + 2;
  const step =
    version === 32
      ? 26
      : Math.ceil((version * 4 + 4) / (numAlign * 2 - 2)) * 2;
  const result: number[] = [6];
  for (let pos = version * 4 + 10; result.length < numAlign; pos -= step) {
    result.splice(1, 0, pos);
  }
  return result;
}

function reserveFormatInfo(
  size: number,
  setFunction: (x: number, y: number, dark: boolean) => void,
  version: number,
): void {
  // Format info near finders (mark as function; values filled later).
  for (let i = 0; i <= 8; i++) {
    if (i !== 6) {
      setFunction(8, i, false);
      setFunction(i, 8, false);
    }
  }
  for (let i = 0; i < 8; i++) {
    setFunction(size - 1 - i, 8, false);
    setFunction(8, size - 1 - i, false);
  }
  setFunction(8, size - 8, true); // dark module

  // Version info blocks (>= v7).
  if (version >= 7) {
    for (let i = 0; i < 18; i++) {
      const a = size - 11 + (i % 3);
      const b = Math.floor(i / 3);
      setFunction(a, b, false);
      setFunction(b, a, false);
    }
  }
}

function addEccAndInterleave(version: number, ecc: Ecc, data: number[]): number[] {
  const eccOrd = ECC_ORDER.indexOf(ecc);
  const numBlocks = NUM_ECC_BLOCKS[eccOrd][version];
  const blockEccLen = ECC_CODEWORDS_PER_BLOCK[eccOrd][version];
  const rawCodewords = Math.floor(getNumRawDataModules(version) / 8);
  const numShortBlocks = numBlocks - (rawCodewords % numBlocks);
  const shortBlockLen = Math.floor(rawCodewords / numBlocks);

  const blocks: number[][] = [];
  const divisor = reedSolomonComputeDivisor(blockEccLen);
  let k = 0;
  for (let i = 0; i < numBlocks; i++) {
    const datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
    const dat = data.slice(k, k + datLen);
    k += datLen;
    const eccBytes = reedSolomonComputeRemainder(dat, divisor);
    if (i < numShortBlocks) dat.push(0); // pad so all blocks align in interleave
    blocks.push(dat.concat(eccBytes));
  }

  const result: number[] = [];
  const maxLen = shortBlockLen + 1;
  for (let i = 0; i < maxLen; i++) {
    for (let j = 0; j < blocks.length; j++) {
      // Skip the padding cell in short blocks' data region.
      if (i !== shortBlockLen - blockEccLen || j >= numShortBlocks) {
        result.push(blocks[j][i]);
      }
    }
  }
  return result;
}

function placeCodewords(
  modules: boolean[][],
  isFunction: boolean[][],
  size: number,
  codewords: number[],
): void {
  let i = 0; // bit index
  for (let right = size - 1; right >= 1; right -= 2) {
    if (right === 6) right = 5;
    for (let vert = 0; vert < size; vert++) {
      for (let j = 0; j < 2; j++) {
        const x = right - j;
        const upward = ((right + 1) & 2) === 0;
        const y = upward ? size - 1 - vert : vert;
        if (!isFunction[y][x] && i < codewords.length * 8) {
          modules[y][x] = ((codewords[i >>> 3] >>> (7 - (i & 7))) & 1) !== 0;
          i++;
        }
      }
    }
  }
}

function applyMask(
  modules: boolean[][],
  isFunction: boolean[][],
  size: number,
  mask: number,
): void {
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      if (isFunction[y][x]) continue;
      let invert = false;
      switch (mask) {
        case 0: invert = (x + y) % 2 === 0; break;
        case 1: invert = y % 2 === 0; break;
        case 2: invert = x % 3 === 0; break;
        case 3: invert = (x + y) % 3 === 0; break;
        case 4: invert = (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0; break;
        case 5: invert = ((x * y) % 2) + ((x * y) % 3) === 0; break;
        case 6: invert = (((x * y) % 2) + ((x * y) % 3)) % 2 === 0; break;
        case 7: invert = (((x + y) % 2) + ((x * y) % 3)) % 2 === 0; break;
      }
      if (invert) modules[y][x] = !modules[y][x];
    }
  }
}

function drawFormatBits(
  modules: boolean[][],
  isFunction: boolean[][],
  size: number,
  ecc: Ecc,
  mask: number,
): void {
  const data = (ECC_FORMAT_BITS[ecc] << 3) | mask;
  let rem = data;
  for (let i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >>> 9) * 0x537);
  const bits = ((data << 10) | rem) ^ 0x5412;

  const setModule = (x: number, y: number, dark: boolean) => {
    modules[y][x] = dark;
    isFunction[y][x] = true;
  };

  for (let i = 0; i <= 5; i++) setModule(8, i, ((bits >>> i) & 1) !== 0);
  setModule(8, 7, ((bits >>> 6) & 1) !== 0);
  setModule(8, 8, ((bits >>> 7) & 1) !== 0);
  setModule(7, 8, ((bits >>> 8) & 1) !== 0);
  for (let i = 9; i < 15; i++) setModule(14 - i, 8, ((bits >>> i) & 1) !== 0);

  for (let i = 0; i < 8; i++) setModule(size - 1 - i, 8, ((bits >>> i) & 1) !== 0);
  for (let i = 8; i < 15; i++) setModule(8, size - 15 + i, ((bits >>> i) & 1) !== 0);
  setModule(8, size - 8, true);
}

function drawVersionInfo(
  modules: boolean[][],
  isFunction: boolean[][],
  size: number,
  version: number,
): void {
  if (version < 7) return;
  let rem = version;
  for (let i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >>> 11) * 0x1f25);
  const bits = (version << 12) | rem;

  for (let i = 0; i < 18; i++) {
    const bit = ((bits >>> i) & 1) !== 0;
    const a = size - 11 + (i % 3);
    const b = Math.floor(i / 3);
    modules[b][a] = bit;
    isFunction[b][a] = true;
    modules[a][b] = bit;
    isFunction[a][b] = true;
  }
}

function computePenalty(modules: boolean[][], size: number): number {
  let penalty = 0;
  // Rule 1: runs of same color in rows/cols.
  for (let y = 0; y < size; y++) {
    let runColor = modules[y][0];
    let runLen = 1;
    for (let x = 1; x < size; x++) {
      if (modules[y][x] === runColor) {
        runLen++;
      } else {
        if (runLen >= 5) penalty += 3 + (runLen - 5);
        runColor = modules[y][x];
        runLen = 1;
      }
    }
    if (runLen >= 5) penalty += 3 + (runLen - 5);
  }
  for (let x = 0; x < size; x++) {
    let runColor = modules[0][x];
    let runLen = 1;
    for (let y = 1; y < size; y++) {
      if (modules[y][x] === runColor) {
        runLen++;
      } else {
        if (runLen >= 5) penalty += 3 + (runLen - 5);
        runColor = modules[y][x];
        runLen = 1;
      }
    }
    if (runLen >= 5) penalty += 3 + (runLen - 5);
  }
  // Rule 2: 2x2 blocks.
  for (let y = 0; y < size - 1; y++) {
    for (let x = 0; x < size - 1; x++) {
      const c = modules[y][x];
      if (c === modules[y][x + 1] && c === modules[y + 1][x] && c === modules[y + 1][x + 1]) {
        penalty += 3;
      }
    }
  }
  // Rule 3: finder-like patterns (approximate; sufficient for mask selection).
  const pattern = [true, false, true, true, true, false, true];
  const hasPattern = (get: (i: number) => boolean, len: number, i: number) => {
    if (i + 7 > len) return false;
    for (let k = 0; k < 7; k++) if (get(i + k) !== pattern[k]) return false;
    return true;
  };
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      if (hasPattern((i) => modules[y][i], size, x)) penalty += 40;
      if (hasPattern((i) => modules[i][x], size, y)) penalty += 40;
    }
  }
  // Rule 4: dark module proportion.
  let dark = 0;
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) if (modules[y][x]) dark++;
  const total = size * size;
  const k = Math.floor((Math.abs(dark * 20 - total * 10) + total - 1) / total) - 1;
  penalty += Math.max(k, 0) * 10;
  return penalty;
}
