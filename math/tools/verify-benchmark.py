"""Independent MPFR directed-rounding oracle for the ln(sin(1)) benchmark.

Development verification only; never used by the .NET library.
Install gmpy2==2.2.1 in a separate Python environment.
Usage: python verify-benchmark.py artifacts/math-benchmarks/ln-sin-1-decimal-1000.txt
"""
import hashlib
import json
import pathlib
import sys
import time

import gmpy2


def verify(path: pathlib.Path) -> dict:
    actual = path.read_text(encoding="utf-8")
    digits = len(actual.partition(".")[2])
    bits = 4 * (digits + 40)  # Integer upper bound on log2(10)*(digits+40).
    scale = gmpy2.mpz(10) ** digits
    endpoints = []
    started = time.perf_counter()
    for rounding in (gmpy2.RoundDown, gmpy2.RoundUp):
        # sin is evaluated at the exact integer 1; ln is monotone on the positive sin enclosure.
        with gmpy2.context(gmpy2.get_context(), precision=bits, round=rounding):
            bound = gmpy2.log(gmpy2.sin(gmpy2.mpfr(1)))
            units = gmpy2.mpz(gmpy2.trunc(bound * scale))
            raw = str(abs(units)).rjust(digits + 1, "0")
            endpoints.append(("-" if units < 0 else "") + raw[:-digits] + "." + raw[-digits:])
    if endpoints[0] != endpoints[1]:
        raise AssertionError("Independent directed bounds do not certify the same decimal digits")
    if actual != endpoints[0]:
        first = next((i for i, (a, b) in enumerate(zip(actual, endpoints[0])) if a != b), None)
        raise AssertionError(f"Independent result mismatch at character {first}")
    return {
        "verified": True, "digits": digits, "oracle": "gmpy2 / MPFR, directed rounding",
        "gmpy2": gmpy2.version(), "mpfr": gmpy2.mpfr_version(), "precision_bits": bits,
        "seconds": time.perf_counter() - started,
        "sha256": hashlib.sha256(actual.encode("utf-8")).hexdigest().upper(),
    }


if __name__ == "__main__":
    report = verify(pathlib.Path(sys.argv[1]))
    target = pathlib.Path(sys.argv[1]).with_suffix(".verification.json")
    target.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
