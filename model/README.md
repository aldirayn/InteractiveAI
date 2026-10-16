---
license: cc-by-nc-4.0
base_model: facebook/mms-tts-ind
library_name: transformers.js
pipeline_tag: text-to-speech
tags: [onnx, transformers.js, tts, vits, mms, ind]
---

# mms-tts-ind - ONNX for Transformers.js

ONNX export of [`facebook/mms-tts-ind`](https://huggingface.co/facebook/mms-tts-ind)
for in-browser inference.

- Recommended weights: **fp32** (114.2 MB)
- Waveform correlation vs the PyTorch reference: **1.00000**
- Sample rate: 16000 Hz | noise scales set to 0 for deterministic output

### Quantization notes

VITS ends in a HiFi-GAN vocoder. Naive dynamic int8 over the whole graph
produces **noise**, not speech - measured below.

**Files that are in this repo.** Two, and no others:

| file | size | status |
|---|---|---|
| `onnx/model.onnx` | 114.2 MB | fp32, correlation +1.00000, **load this one** |
| `onnx/model_fp16.onnx` | 58.2 MB | fp16, never passed, **do not use** |

`model_fp16.onnx` went up in the same commit as the fp32 export. It is in the
repo because that commit put it there, not because it was measured and cleared.
Every fp16 conversion tried in this export failed to load: `onnxconverter_common`
could not place the fp16/fp32 boundaries consistently across a VITS graph, and
three strategies each broke at a different node. There is no correlation figure
for it because inference never ran. Do not reach for it because it is half the
size. The demo site asks for `dtype: 'fp32'` on the wasm backend and never loads
it.

**Variants that were measured and never uploaded.** These three were built and
scored during Phase A, all three failed parity, and none of them was pushed. No
file in the table below exists in this repo. The correlations are real
measurements and are kept because they answer the question a reader actually
has, which is whether a smaller download is available here. It is not.

| variant | size when built | corr | verdict | in this repo |
|---|---|---|---|---|
| `int8_all` | 38.4 MB | +0.1911 | FAIL | no |
| `int8_no_decoder` | 73.2 MB | +0.2086 | FAIL | no |
| `int8_per_channel` | 38.4 MB | -0.0500 | FAIL | no |

fp32 is the only file here that passes parity, so fp32 is the only file to load.

## Correction history

Earlier versions of this card listed the three int8 variants in the same table
as fp32 and closed with a line saying the others "are included for reference
only". They were not included. `int8_all`, `int8_no_decoder` and
`int8_per_channel` were built, measured, failed parity and were never pushed.
The repo has held `onnx/model.onnx` and `onnx/model_fp16.onnx` and nothing else
since the first upload on 11 August 2026. The measurements were real; the files
were never here to download. On 13 August 2026 the section was split in two so
that what was measured and what is downloadable are no longer the same table.

Those versions also said nothing at all about `onnx/model_fp16.onnx`, which is
in the repo, was never cleared, and does not load. It is listed now.

## Usage

```js
import { pipeline } from '@huggingface/transformers'
const tts = await pipeline('text-to-speech', 'souba67/mms-tts-ind-ONNX',
                           { dtype: 'fp32' })
const out = await tts('Selamat datang')
```

Original model by Meta AI (Massively Multilingual Speech). Licence CC-BY-NC 4.0
carries over: **non-commercial use only**.
