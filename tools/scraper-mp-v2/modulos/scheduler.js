import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..');
const PID_FILE = path.join(ROOT, '.daemon.pid');

let running = false;
let currentTimeout = null;

export function isDaemonMode() {
  return process.argv.includes('--daemon');
}

export function isIncrementalMode() {
  return process.argv.includes('--incremental');
}

export function writePidFile() {
  try {
    fs.writeFileSync(PID_FILE, String(process.pid));
    console.log(`[SCHEDULER] PID file creado: ${PID_FILE} (PID: ${process.pid})`);
  } catch (e) {
    console.log(`[SCHEDULER] Error creando PID file: ${e.message}`);
  }
}

export function removePidFile() {
  try {
    if (fs.existsSync(PID_FILE)) {
      fs.unlinkSync(PID_FILE);
    }
  } catch (e) {
    // no hacer nada
  }
}

export function checkExistingProcess() {
  try {
    if (fs.existsSync(PID_FILE)) {
      const oldPid = parseInt(fs.readFileSync(PID_FILE, 'utf-8').trim(), 10);
      if (oldPid > 0) {
        try {
          process.kill(oldPid, 0);
          console.log(`[SCHEDULER] Ya hay un proceso corriendo (PID: ${oldPid}). Saliendo.`);
          return true;
        } catch (e) {
          console.log(`[SCHEDULER] PID file huérfano (${oldPid}), ignorando.`);
          removePidFile();
        }
      }
    }
  } catch (e) {
    // no hacer nada
  }
  return false;
}

export function setupSignalHandlers(stopCallback) {
  const shutdown = async (signal) => {
    console.log(`\n[SCHEDULER] Señal ${signal} recibida. Deteniendo...`);
    running = false;
    if (currentTimeout) {
      clearTimeout(currentTimeout);
      currentTimeout = null;
    }
    if (stopCallback) await stopCallback();
    removePidFile();
    process.exit(0);
  };

  process.on('SIGINT', () => shutdown('SIGINT'));
  process.on('SIGTERM', () => shutdown('SIGTERM'));
  process.on('uncaughtException', (e) => {
    console.log(`[SCHEDULER] Error no capturado: ${e.message}`);
    removePidFile();
    process.exit(1);
  });
}

export function startDaemon(executeCycle, getIntervalMs) {
  running = true;
  writePidFile();

  console.log(`\n${'='.repeat(60)}`);
  console.log('  SCHEDULER - AGENTE MERCADO PUBLICO');
  console.log(`  Modo: DAEMON (cada ${Math.round(getIntervalMs() / 3600000)} horas)`);
  console.log(`  PID: ${process.pid}`);
  console.log(`${'='.repeat(60)}\n`);

  async function cycle() {
    if (!running) return;

    const startTime = Date.now();
    console.log(`\n${'─'.repeat(50)}`);
    console.log(`[SCHEDULER] Ciclo iniciado: ${new Date().toISOString()}`);
    console.log(`${'─'.repeat(50)}\n`);

    try {
      await executeCycle();
    } catch (e) {
      console.log(`[SCHEDULER] Error en ciclo: ${e.message}`);
    }

    const elapsed = Date.now() - startTime;
    const nextRun = getIntervalMs();

    console.log(`\n[SCHEDULER] Ciclo completado en ${Math.round(elapsed / 1000)}s`);
    console.log(`[SCHEDULER] Próximo ciclo en ${Math.round(nextRun / 3600000)} horas`);

    if (running) {
      currentTimeout = setTimeout(cycle, nextRun);
      currentTimeout.unref();
    }
  }

  cycle();
}