import type { DemoPersona } from './types';

const key = 'many-rooms.demo-entered';
const personaKey = 'many-rooms.demo-persona';
let enteredWithoutStorage = false;
let personaWithoutStorage: DemoPersona = 'volunteer';
// This flag controls navigation only. It is never a credential or user identity.
export const demoSession = {
  entered: () => {
    try {
      return localStorage.getItem(key) === 'true';
    } catch {
      return enteredWithoutStorage;
    }
  },
  persona: (): DemoPersona => {
    try {
      return localStorage.getItem(personaKey) === 'leader'
        ? 'leader'
        : 'volunteer';
    } catch {
      return personaWithoutStorage;
    }
  },
  enter: (persona: DemoPersona = 'volunteer') => {
    enteredWithoutStorage = true;
    personaWithoutStorage = persona;
    try {
      localStorage.setItem(key, 'true');
      localStorage.setItem(personaKey, persona);
    } catch {
      /* Private browsing can disable storage; current navigation still works. */
    }
  },
  leave: () => {
    enteredWithoutStorage = false;
    personaWithoutStorage = 'volunteer';
    try {
      localStorage.removeItem(key);
      localStorage.removeItem(personaKey);
    } catch {
      /* No session remains when storage is unavailable. */
    }
  },
};
