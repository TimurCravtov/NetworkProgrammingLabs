import requests
import time
import matplotlib.pyplot as plt
import subprocess
import os
import random
import numpy as np  # Used for percentile calculation
from concurrent.futures import ThreadPoolExecutor

# Configuration
LEADER_URL = "http://localhost:8080"
FOLLOWER_URLS = [f"http://localhost:{8081+i}" for i in range(5)]
NUM_WRITES = 200
NUM_KEYS = 10
CONCURRENCY = 20  # >10 threads

def run_command(cmd, env=None, cwd=None):
    subprocess.run(cmd, shell=True, check=True, env=env, cwd=cwd)

def write_task(key, value):
    start = time.time()
    try:
        resp = requests.post(f"{LEADER_URL}/put", json={"key": key, "value": value}, timeout=10)
        latency = time.time() - start
        return resp.status_code == 200, latency
    except Exception as e:
        # print(f"Write error: {e}")
        return False, time.time() - start

def check_consistency():
    mismatches = 0
    print("Verifying consistency...")

    # Get all data from Leader
    try:
        resp = requests.get(f"{LEADER_URL}/getall", timeout=2)
        leader_data = resp.json() if resp.status_code == 200 else {}
    except:
        leader_data = {}

    # Check Followers
    for f_idx, f_url in enumerate(FOLLOWER_URLS):
        try:
            resp = requests.get(f"{f_url}/getall", timeout=2)
            follower_data = resp.json() if resp.status_code == 200 else {}
        except:
            follower_data = {}

        # Compare key by key
        for key, leader_val in leader_data.items():
            follower_val = follower_data.get(key)
            if leader_val != follower_val:
                mismatches += 1
                # print(f"Mismatch Key={key}: Leader={leader_val} Follower{f_idx+1}={follower_val}")

    return mismatches

def check_consistency_getall():
    mismatches = 0
    print("Verifying consistency (using /getall)...")
    
    # Get Leader Data
    leader_data = {}
    try:
        resp = requests.get(f"{LEADER_URL}/getall", timeout=5)
        if resp.status_code == 200:
            leader_data = resp.json()
        else:
            print(f"Failed to get all data from Leader: {resp.status_code}")
            return -1 # Error
    except Exception as e:
        print(f"Exception getting data from Leader: {e}")
        return -1

    # Check Followers
    for f_idx, f_url in enumerate(FOLLOWER_URLS):
        follower_data = {}
        try:
            resp = requests.get(f"{f_url}/getall", timeout=5)
            if resp.status_code == 200:
                follower_data = resp.json()
            else:
                print(f"Failed to get all data from Follower {f_idx+1}: {resp.status_code}")
                mismatches += 1
                continue
        except Exception as e:
            print(f"Exception getting data from Follower {f_idx+1}: {e}")
            mismatches += 1
            continue
            
        # Compare dictionaries
        if leader_data != follower_data:
            mismatches += 1

    return mismatches

def run_experiment():
    # Move to parent directory where docker-compose.yml is located
    project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    
    quorums = [1, 2, 3, 4, 5]
    results = [] # Stores mean, p50, p95, and p99 for each quorum
    
    for q in quorums:
        print(f"\n=== Testing Write Quorum: {q} ===")
        env = os.environ.copy()
        env["WRITE_QUORUM"] = str(q)
        
        print("Restarting cluster...")
        try:
            # Down first to clear memory
            run_command("docker compose down", env=env, cwd=project_root)
            run_command("docker compose up -d", env=env, cwd=project_root)
        except subprocess.CalledProcessError as e:
            print(f"Docker command failed: {e}")
            return

        print("Waiting for leader to be ready...")
        time.sleep(15) # Wait for dotnet to start
        
        keys = [f"key-{i}" for i in range(NUM_KEYS)]
        latencies = []
        
        print(f"Performing {NUM_WRITES} writes with {CONCURRENCY} threads...")
        
        with ThreadPoolExecutor(max_workers=CONCURRENCY) as executor:
            futures = []
            for i in range(NUM_WRITES):
                key = random.choice(keys)
                val = f"value-{i}"
                futures.append(executor.submit(write_task, key, val))
            
            completed = 0
            for f in futures:
                success, lat = f.result()
                if success:
                    latencies.append(lat)
                completed += 1
                if completed % 1000 == 0:
                    print(f"Completed {completed}/{NUM_WRITES} requests")
        
        if latencies:
            # Calculate Mean, Median (P50), P95, P99
            latencies_array = np.array(latencies)
            mean_lat = np.mean(latencies_array)
            p50_lat = np.percentile(latencies_array, 50) # Median
            p95_lat = np.percentile(latencies_array, 95)
            p99_lat = np.percentile(latencies_array, 99)
            
            results.append({
                'quorum': q,
                'mean': mean_lat,
                'p50': p50_lat,
                'p95': p95_lat,
                'p99': p99_lat
            })
            
            print(f"Mean: {mean_lat:.4f}s, Median (P50): {p50_lat:.4f}s, P95: {p95_lat:.4f}s, P99: {p99_lat:.4f}s")
        else:
            results.append({'quorum': q, 'mean': 0, 'p50': 0, 'p95': 0, 'p99': 0})
            print("All writes failed.")

        mismatches = check_consistency_getall()
        print(f"Consistency check: {mismatches} mismatches found.")
        
        if mismatches == 0:
            print("Result: Strong consistency observed (Data matches).")
        else:
            print("Result: Inconsistency observed.")

    # Plotting
    
    # Prepare data for plotting
    plot_quorums = [r['quorum'] for r in results]
    mean_latencies = [r['mean'] for r in results]
    p50_latencies = [r['p50'] for r in results]
    p95_latencies = [r['p95'] for r in results]
    p99_latencies = [r['p99'] for r in results]
    
    plt.figure(figsize=(10, 6))
    
    # Plot all four metrics
    plt.plot(plot_quorums, mean_latencies, marker='o', linestyle='-', color='b', label='Mean Latency (Average)')
    plt.plot(plot_quorums, p50_latencies, marker='d', linestyle='-', color='y', label='Median Latency (P50)') # Added median
    plt.plot(plot_quorums, p95_latencies, marker='s', linestyle='--', color='r', label='P95 Latency')
    plt.plot(plot_quorums, p99_latencies, marker='^', linestyle=':', color='g', label='P99 Latency')
    
    plt.title('Write Quorum vs Latency Statistics')
    plt.xlabel('Write Quorum (Number of Followers)')
    plt.ylabel('Latency (seconds)')
    plt.legend() 
    plt.grid(True)
    
    output_file = os.path.join(os.path.dirname(__file__), 'latency_statistics_plot_full.png')
    plt.savefig(output_file)
    print(f"\nPlot saved to {output_file}")
    
if __name__ == "__main__":
    run_experiment()