import { useCallback, useRef } from "react";
import { debounce } from "lodash";

export default function useDebounce(callback, delay) {
    const callbackRef = useRef(callback);

    callbackRef.current = callback;

    const debouncedFunc = debounce((...args) => callbackRef.current(...args), delay);
    
    return useCallback(debouncedFunc,[]);
}