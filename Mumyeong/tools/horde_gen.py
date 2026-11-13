"""Runs image jobs on AI Horde (aihorde.net, volunteer GPUs, anonymous key) — the service 엔시페르's card art came from
(docs/card-art-credits.md). Used for the 무명 painted art; prompts and settings are kept in tools/art_jobs/*.json and
listed in docs/mumyeong-art-credits.md.

Job file: {"name": {"p": prompt, "neg": negative prompt, "w": 1024, "h": 576, "seed": 1, "model": "...",
                   "pp": ["RealESRGAN_x2plus", "strip_background"], "steps": 30}, ...}
Usage: python3 tools/horde_gen.py tools/art_jobs/backgrounds.json OUT_DIR [name ...]
Already downloaded names are skipped. Up to 4 jobs run at once.
"""
import json
import os
import sys
import time
import urllib.request

API = "https://aihorde.net/api/v2"
KEY = "0000000000"  # anonymous
DEFAULT_MODEL = "AlbedoBase XL (SDXL)"


def call(method, path, body=None):
    req = urllib.request.Request(API + path, method=method, headers={
        "apikey": KEY, "Content-Type": "application/json", "Client-Agent": "mumyeong-mod-art:1.0:anonymous",
        "User-Agent": "mumyeong-mod-art/1.0"})
    data = json.dumps(body).encode() if body is not None else None
    with urllib.request.urlopen(req, data=data, timeout=60) as r:
        return json.loads(r.read())


def submit(job):
    params = {"width": job.get("w", 1024), "height": job.get("h", 576), "steps": job.get("steps", 30),
              "cfg_scale": job.get("cfg", 6.5), "sampler_name": job.get("sampler", "k_dpmpp_2m"),
              "seed": str(job.get("seed", 1)), "n": 1, "karras": True}
    if job.get("pp"):
        params["post_processing"] = job["pp"]
    body = {"prompt": job["p"] + (" ### " + job["neg"] if job.get("neg") else ""), "params": params,
            "models": [job.get("model", DEFAULT_MODEL)], "nsfw": False, "censor_nsfw": True, "r2": True,
            "trusted_workers": False, "slow_workers": True}
    return call("POST", "/generate/async", body)["id"]


def main():
    jobs = json.load(open(sys.argv[1], encoding="utf-8"))
    out = sys.argv[2]
    only = set(sys.argv[3:])
    os.makedirs(out, exist_ok=True)
    todo = [n for n in jobs if (not only or n in only) and not os.path.exists(os.path.join(out, n + ".webp"))]
    running = {}
    failures = {}
    while todo or running:
        while todo and len(running) < 4:
            name = todo.pop(0)
            try:
                running[name] = submit(jobs[name])
                print("submitted", name, flush=True)
            except Exception as e:  # rate limit / busy: retry later
                print("submit failed", name, e, flush=True)
                failures[name] = failures.get(name, 0) + 1
                if failures[name] < 6:
                    todo.append(name)
                time.sleep(10)
                break
        time.sleep(8)
        for name, rid in list(running.items()):
            try:
                check = call("GET", f"/generate/check/{rid}")
                if check.get("faulted"):
                    print("faulted", name, flush=True)
                    del running[name]
                    todo.append(name)
                    continue
                if not check.get("done"):
                    continue
                status = call("GET", f"/generate/status/{rid}")
                gens = status.get("generations") or []
                if not gens:
                    print("no image", name, status.get("message"), flush=True)
                    del running[name]
                    continue
                url = gens[0]["img"]
                with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "mumyeong-mod-art/1.0"}), timeout=120) as r, open(os.path.join(out, name + ".webp"), "wb") as f:
                    f.write(r.read())
                meta = {"name": name, "model": gens[0].get("model"), "worker": gens[0].get("worker_name"),
                        "seed": gens[0].get("seed"), "job": jobs[name]}
                with open(os.path.join(out, name + ".json"), "w", encoding="utf-8") as f:
                    json.dump(meta, f, ensure_ascii=False, indent=1)
                print("done", name, gens[0].get("model"), flush=True)
                del running[name]
            except Exception as e:
                print("poll error", name, e, flush=True)


if __name__ == "__main__":
    main()
