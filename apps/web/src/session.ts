const key = 'many-rooms.demo-entered';
let enteredWithoutStorage = false;
// This flag controls navigation only. It is never a credential or user identity.
export const demoSession = {
  entered: () => {
    try {
      return localStorage.getItem(key) === 'true';
    } catch {
      return enteredWithoutStorage;
    }
  },
  enter: () => {
    enteredWithoutStorage = true;
    try {
      localStorage.setItem(key, 'true');
    } catch {
      /* Private browsing can disable storage; current navigation still works. */
    }
  },
  leave: () => {
    enteredWithoutStorage = false;
    try {
      localStorage.removeItem(key);
    } catch {
      /* No session remains when storage is unavailable. */
    }
  },
};
